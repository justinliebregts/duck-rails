using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("LIV.LCK")]
[assembly: InternalsVisibleTo("LIV.LCK.Streaming")]
[assembly: InternalsVisibleTo("LIV.LCK.Tests.Common")]
[assembly: InternalsVisibleTo("LIV.LCK.PlayModeTests")]
[assembly: InternalsVisibleTo("LIV.LCK.EditModeTests")]
[assembly: InternalsVisibleTo("LIV.LCK.Streaming.PlayModeTests")]
[assembly: InternalsVisibleTo("LIV.LCK.Streaming.EditModeTests")]

// Allow Moq to use internals:
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]