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
    
    [Signature("48 83 EC 38 F3 0F 10 05 ?? ?? ?? ?? 45 33 C9", DetourName = nameof(ShouldSnapUnsitDetour))]
    private Hook<ShouldSnap>? ShouldSnapUnsitHook { get; init; } = null!;

    private bool suppressedSnap;
    
    private byte ShouldSnapDetour(float* a1, float* a2) => (byte) (suppressedSnap ? 0 : ShouldSnapHook!.Original(a1, a2));
    private byte ShouldSnapUnsitDetour(float* a1, float* a2) => 0;
    
    [PluginService] public static CommandManager CommandManager { get; private set; } = null!;


    public Plugin() {
        SignatureHelper.Initialise(this);
        ShouldSnapHook?.Enable();
        ShouldSnapUnsitHook?.Enable();

        CommandManager.AddHandler("/dozeanywhere", new CommandInfo(DozeAnywhere) {
            HelpMessage = "Doze as if on a bed, even with no bed around. Use '/dozeanywhere nosnap' to disable snapping"
        });

        CommandManager.AddHandler("/sitanywhere", new CommandInfo(SitAnywhere) {
            HelpMessage = "Sit as if on a chair, even with no chair around."
        });
    }

    public void Dispose() {
        ShouldSnapHook?.Dispose();
        ShouldSnapUnsitHook?.Dispose();
        CommandManager.RemoveHandler("/dozeanywhere");
        CommandManager.RemoveHandler("/sitanywhere");
    }

    private void DozeAnywhere(string command, string args) {
        var agent = AgentModule.Instance()->GetAgentByInternalId(AgentId.Emote);
        if (args.Contains("nosnap", StringComparison.InvariantCultureIgnoreCase)) suppressedSnap = true;
        useEmote(new IntPtr(agent), 88, IntPtr.Zero, 0, 0);
        suppressedSnap = false;
    }

    private void SitAnywhere(string command, string args) {
        var agent = AgentModule.Instance()->GetAgentByInternalId(AgentId.Emote);
        useEmote(new IntPtr(agent), 96, IntPtr.Zero, 0, 0);
    }
}
