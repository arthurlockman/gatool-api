using Amazon.CDK;

namespace InfraCdk;

public static class Program
{
    public static void Main()
    {
        var app = new App();

        var env = new Amazon.CDK.Environment
        {
            Region = app.Node.TryGetContext("region")?.ToString() ?? "us-east-2"
        };

        new GatoolStack(app, "GatoolStack", new StackProps { Env = env });

        app.Synth();
    }
}
