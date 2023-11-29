namespace Libplanet.Headless.Hosting;

public class PluginActionEvaluatorConfiguration : IActionEvaluatorConfiguration
{
    public ActionEvaluatorType Type => ActionEvaluatorType.PluginActionEvaluator;

    public string PluginTypeName { get; set; }

    public string PluginPath { get; init; }
}
