using AspireAiStack.Web;
using AspireAiStack.Web.Components;
using Bunit;

namespace AspireAiStack.Tests;

public sealed class ComponentTests : BunitContext
{
    [Fact]
    public void StackStatusCard_RendersOperationalBoundaries()
    {
        var status = new StackStatus("simulated", "Qdrant", "Redis", SafeDefault: true);

        var cut = Render<StackStatusCard>(parameters => parameters
            .Add(component => component.Status, status));

        Assert.Contains("simulated", cut.Markup);
        Assert.Contains("Qdrant", cut.Markup);
        Assert.Contains("Redis", cut.Markup);
        Assert.Contains("Secrets required", cut.Markup);
        Assert.Contains("No", cut.Markup);
    }
}
