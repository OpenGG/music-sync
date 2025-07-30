using MusicSync.Models;
using YamlDotNet.Serialization;

namespace MusicSync.Utils
{
    [YamlStaticContext]
    [YamlSerializable(typeof(Config))]
    public partial class ConfigYamlContext : YamlDotNet.Serialization.StaticContext
    {
    }
}