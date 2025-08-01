using MusicSync.Models;
using YamlDotNet.Serialization;

namespace MusicSync.Utils;

[YamlStaticContext]
[YamlSerializable(typeof(Config))]
[YamlSerializable(typeof(DrmPluginConfig))]
public partial class ConfigYamlContext;
