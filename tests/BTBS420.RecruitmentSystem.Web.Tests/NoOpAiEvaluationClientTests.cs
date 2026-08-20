using BTBS420.RecruitmentSystem.Web.Ai;
using Microsoft.Extensions.DependencyInjection;

namespace BTBS420.RecruitmentSystem.Web.Tests;

public sealed class NoOpAiEvaluationClientTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public NoOpAiEvaluationClientTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task EvaluateAsync_MetadataProviderNone_DonerVeAgaCikmaz()
    {
        var client = new NoOpAiEvaluationClient();
        var request = new AiEvaluationRequest(Prompt: "test");

        var result = await client.EvaluateAsync(request);

        Assert.Equal("None", result.Metadata.Provider);
        Assert.Equal("none", result.Metadata.Model);
        Assert.Null(result.Metadata.ModelVersion);
        Assert.Null(result.ParsedJson);
        Assert.Equal(string.Empty, result.RawText);
    }

    [Fact]
    public async Task EvaluateAsync_IptalEdilmisTokenIleOperationCanceledFirlatir()
    {
        var client = new NoOpAiEvaluationClient();
        var request = new AiEvaluationRequest(Prompt: "test");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => client.EvaluateAsync(request, cts.Token));
    }

    [Fact]
    public void Di_VarsayilanKayitNoOpAiEvaluationClientaCozumlenir()
    {
        using var scope = _factory.Services.CreateScope();

        var client = scope.ServiceProvider.GetRequiredService<IAiEvaluationClient>();

        Assert.IsType<NoOpAiEvaluationClient>(client);
    }
}
