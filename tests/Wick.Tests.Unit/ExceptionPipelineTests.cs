using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Wick.Core;

namespace Wick.Tests.Unit;

public sealed class ExceptionPipelineTests
{
    private static RawException MakeRaw(string type = "System.Exception", string message = "boom") => new()
    {
        Type = type,
        Message = message,
        RawText = $"ERROR: {type}: {message}",
        Frames =
        [
            new ExceptionFrame("MyApp.Foo.Bar()", "/src/Foo.cs", 42, IsUserCode: true),
        ],
    };

    private static ExceptionEnricher MakeEnricher(IRoslynWorkspaceService? workspace = null)
    {
        workspace ??= Substitute.For<IRoslynWorkspaceService>();
        return new ExceptionEnricher(workspace, new LogBuffer(), bridge: null);
    }

    private static ExceptionPipeline MakePipeline(
        IEnumerable<IExceptionSource> sources,
        ExceptionEnricher? enricher = null,
        ExceptionBuffer? buffer = null)
    {
        enricher ??= MakeEnricher();
        buffer ??= new ExceptionBuffer();
        return new ExceptionPipeline(
            sources,
            enricher,
            buffer,
            NullLogger<ExceptionPipeline>.Instance);
    }

    /// <summary>
    /// Polls the buffer's count every 25ms up to <paramref name="timeoutMs"/> until it
    /// reaches at least <paramref name="expectedCount"/>. Replaces the old fixed
    /// <c>Task.Delay(200)</c>-then-assert pattern, which was flaky on slower CI
    /// runners (Windows in particular). The 5-second cap is generous enough for any
    /// real CI host while still bounding a genuine bug to a tractable wait.
    /// </summary>
    private static async Task WaitForBufferCountAsync(
        ExceptionBuffer buffer,
        int expectedCount,
        CancellationToken ct,
        int timeoutMs = 5000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (buffer.Count >= expectedCount) return;
            try
            {
                await Task.Delay(25, ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    [Fact]
    public async Task Buffer_ReceivesEnrichedException()
    {
        var raw = MakeRaw();
        var source = new TestExceptionSource([raw]);
        var buffer = new ExceptionBuffer();

        var pipeline = MakePipeline([source], buffer: buffer);

        using var cts = new CancellationTokenSource();
        await pipeline.StartAsync(cts.Token);

        // Wait for the background task to consume the single injected exception.
        await WaitForBufferCountAsync(buffer, 1, TestContext.Current.CancellationToken);

        await cts.CancelAsync();
        await pipeline.StopAsync(TestContext.Current.CancellationToken);

        buffer.Count.Should().Be(1);
        var items = buffer.GetAll();
        items[0].Raw.Should().BeSameAs(raw);
    }

    [Fact]
    public async Task EnrichmentFailure_FallsBackToRawOnly()
    {
        var raw = MakeRaw();
        var source = new TestExceptionSource([raw]);
        var buffer = new ExceptionBuffer();

        // Wire up a workspace that throws when the enricher tries to use it
        var workspace = Substitute.For<IRoslynWorkspaceService>();
        workspace.IsLoaded.Returns(true);
        workspace.GetSourceContext(Arg.Any<string>(), Arg.Any<int>())
            .Throws(new InvalidOperationException("workspace exploded"));

        var enricher = new ExceptionEnricher(workspace, new LogBuffer(), bridge: null);
        var pipeline = MakePipeline([source], enricher, buffer);

        using var cts = new CancellationTokenSource();
        await pipeline.StartAsync(cts.Token);
        await WaitForBufferCountAsync(buffer, 1, TestContext.Current.CancellationToken);

        await cts.CancelAsync();
        await pipeline.StopAsync(TestContext.Current.CancellationToken);

        buffer.Count.Should().Be(1);
        var items = buffer.GetAll();
        items[0].Raw.Should().BeSameAs(raw);
        items[0].Source.Should().BeNull("enrichment failed so source context should be absent");
    }

    [Fact]
    public async Task Cancellation_TerminatesCleanly()
    {
        var source = new NeverEndingExceptionSource();
        var buffer = new ExceptionBuffer();
        var pipeline = MakePipeline([source], buffer: buffer);

        using var cts = new CancellationTokenSource();
        await pipeline.StartAsync(cts.Token);

        // Wait until the never-ending source has yielded at least one exception
        // (the original 350ms fixed sleep was flaky on Windows CI).
        await WaitForBufferCountAsync(buffer, 1, TestContext.Current.CancellationToken);
        var countBefore = buffer.Count;
        countBefore.Should().BeGreaterThan(0, "source should have yielded at least one exception");

        await cts.CancelAsync();

        // StopAsync should complete without hanging
        var stopTask = pipeline.StopAsync(TestContext.Current.CancellationToken);
#pragma warning disable xUnit1051 // Intentional: timeout guard must not use test CT
        var completed = await Task.WhenAny(stopTask, Task.Delay(3000));
#pragma warning restore xUnit1051
        completed.Should().BeSameAs(stopTask, "pipeline should stop within a reasonable timeout");
    }

    [Fact]
    public async Task MultipleSources_ConsumedInParallel()
    {
        var raw1 = MakeRaw("System.ArgumentException", "bad arg");
        var raw2 = MakeRaw("System.InvalidOperationException", "bad state");
        var source1 = new TestExceptionSource([raw1]);
        var source2 = new TestExceptionSource([raw2]);
        var buffer = new ExceptionBuffer();

        var pipeline = MakePipeline([source1, source2], buffer: buffer);

        using var cts = new CancellationTokenSource();
        await pipeline.StartAsync(cts.Token);
        await WaitForBufferCountAsync(buffer, 2, TestContext.Current.CancellationToken);

        await cts.CancelAsync();
        await pipeline.StopAsync(TestContext.Current.CancellationToken);

        buffer.Count.Should().Be(2);
        var all = buffer.GetAll();
        all.Select(e => e.Raw.Message).Should().BeEquivalentTo(["bad arg", "bad state"]);
    }
}

internal sealed class TestExceptionSource : IExceptionSource
{
    private readonly RawException[] _exceptions;

    public TestExceptionSource(RawException[] exceptions) => _exceptions = exceptions;

    public async IAsyncEnumerable<RawException> CaptureAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        foreach (var ex in _exceptions)
        {
            yield return ex;
            await Task.Yield();
        }
    }
}

internal sealed class NeverEndingExceptionSource : IExceptionSource
{
    public async IAsyncEnumerable<RawException> CaptureAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(100, ct);
            yield return new RawException
            {
                Type = "Test",
                Message = "Test",
                RawText = "Test",
                Frames = [],
            };
        }
    }
}
