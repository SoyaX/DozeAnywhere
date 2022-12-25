using System;
using Dalamud.Game.Command;
using Dalamud.Hooking;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace DozeAnywhere;

public sealed unsafe class Plugin : IDalamudPlugin {
    public string Name => "Doze Anywhere";
    
    [Signature("E8 ?? ?? ?? ?? 4C 8B 74 24 ?? 48 8B CE E8")]
    private readonly delegate* unmanaged<IntPtr, ushort, IntPtr, byte, byte, void> useEmote = null!;
    
    private delegate byte ShouldSnap(float* a1, float* a2);
    
    [Signature("E8 ?? ?? ?? ?? 84 C0 74 44 4C 8D 6D C7", DetourName = nameof(ShouldSnapDetour))]
    private Hook<ShouldSnap>? ShouldSnapHook { get; init; } = null;
    
    private bool suppressedSnap;
    
    private byte ShouldSnapDetour(float* a1, float* a2) => (byte) (suppressedSnap ? 0 : ShouldSnapHook!.Original(a1, a2));
    
    [PluginService] public static CommandManager CommandManager { get; private set; } = null!;


    public Plugin() {
        SignatureHelper.Initialise(this);
        ShouldSnapHook?.Enable();

        CommandManager.AddHandler("/dozeanywhere", new CommandInfo(DozeAnywhere) {
            HelpMessage = "Doze as if on a bed, even with no bed around. Use '/dozeanywhere nosnap' to disable snapping"
        });
    }

    public void Dispose() {
        ShouldSnapHook?.Dispose();
        CommandManager.RemoveHandler("/dozeanywhere");
    }

    private void DozeAnywhere(string command, string args) {
        var agent = AgentModule.Instance()->GetAgentByInternalId(AgentId.Emote);
        if (args.Contains("nosnap", StringComparison.InvariantCultureIgnoreCase)) suppressedSnap = true;
        useEmote(new IntPtr(agent), 88, IntPtr.Zero, 0, 0);
        suppressedSnap = false;
    }
}
