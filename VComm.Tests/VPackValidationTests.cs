using VComm.Core.Functions;
using VComm.Core.Objects;
using Xunit;

namespace VComm.Tests;

public sealed class VPackValidationTests
{
    [Fact]
    public void AcceptsPackProducedByVisualBuilderContract()
    {
        VPack pack = new VPack
        {
            name = "Tactical test",
            author = "Player",
            vRequests = new List<VRequest>
            {
                new VRequest
                {
                    phrases = new[] { "stack up", "form on me" },
                    macro = new Macro
                    {
                        msToWait = 100,
                        keycodes = new List<string> { "(HOLD){CTRL}", "{X}", "{MMB}" }
                    }
                }
            }
        };

        Assert.Empty(Data.GetVPackValidationErrors(pack));
    }

    [Fact]
    public void ReportsCommandAndActionThatNeedAttention()
    {
        VPack pack = new VPack
        {
            name = "Broken pack",
            vRequests = new List<VRequest>
            {
                new VRequest
                {
                    phrases = Array.Empty<string>(),
                    macro = new Macro { msToWait = -1, keycodes = new List<string> { "{NOT_A_KEY}" } }
                }
            }
        };

        IReadOnlyList<string> errors = Data.GetVPackValidationErrors(pack);

        Assert.Contains(errors, error => error.Contains("Command 1", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("action 1", StringComparison.Ordinal));
        Assert.Equal(3, errors.Count);
    }

    [Fact]
    public void RejectsPhraseSharedByDifferentCommands()
    {
        VPack pack = new VPack
        {
            name = "Ambiguous pack",
            vRequests = new List<VRequest>
            {
                new VRequest { phrases = new[] { "reinforce" }, macro = new Macro { keycodes = new List<string> { "{F1}" } } },
                new VRequest { phrases = new[] { "Reinforce" }, macro = new Macro { keycodes = new List<string> { "{F2}" } } }
            }
        };

        Assert.Contains(Data.GetVPackValidationErrors(pack), error => error.Contains("both Command 1 and Command 2"));
    }
}
