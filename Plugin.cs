using System;
using System.Runtime.InteropServices;
using Dalamud.Game.Command;
using Dalamud.Hooking;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Common.Math;

namespace DozeAnywhere;

public sealed unsafe class Plugin : IDalamudPlugin {
    [Signature("E8 ?? ?? ?? ?? 40 84 ED 74 ?? 48 8B 4B ?? 48 8B 01 FF 90")]
    private readonly delegate* unmanaged<IntPtr, ushort, IntPtr, byte, byte, void> useEmote = null!;
    
    private delegate byte ShouldSnap(Character* a1, SnapPosition* a2);
    
    [Signature("E8 ?? ?? ?? ?? 84 C0 0F 84 ?? ?? ?? ?? 4C 8D 74 24", DetourName = nameof(ShouldSnapDetour))]
    private Hook<ShouldSnap>? ShouldSnapHook { get; init; } = null;
    
    [Signature("48 83 EC 38 F3 0F 10 05 ?? ?? ?? ?? 45 33 C9", DetourName = nameof(ShouldSnapUnsitDetour))]
    private Hook<ShouldSnap>? ShouldSnapUnsitHook { get; init; } = null!;

    private bool suppressedSnap;
    
    private byte ShouldSnapDetour(Character* a1, SnapPosition* a2) => (byte) (suppressedSnap ? 0 : ShouldSnapHook!.Original(a1, a2));

    private byte ShouldSnapUnsitDetour(Character* player, SnapPosition* snapPosition)
    {
        var orig = ShouldSnapUnsitHook!.Original(player, snapPosition);
        
        if (orig != 0)
        {
            if (saveSitPosition != null && saveSitRotation != null)
            {
                if (Vector3.Distance(player->GameObject.Position, saveSitPosition.Value) < 3f)
                {
                    snapPosition->PositionB.X = saveSitPosition.Value.X;
                    snapPosition->PositionB.Y = saveSitPosition.Value.Y;
                    snapPosition->PositionB.Z = saveSitPosition.Value.Z;
                    snapPosition->RotationB = saveSitRotation.Value;
                }
                
                saveSitPosition = null;
                saveSitRotation = null;
            }
        }

        return orig;
    }
    
    [PluginService] public static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] public static IClientState ClientState { get; private set; } = null!;
    [PluginService] public static IGameInteropProvider GameInteropProvider { get; private set; } = null!;

    [StructLayout(LayoutKind.Explicit, Size = 0x38)]
    public struct SnapPosition
    {
        [FieldOffset(0x00)]
        public Vector3 PositionA;

        [FieldOffset(0x10)]
        public float RotationA;
        
        [FieldOffset(0x20)] public Vector3 PositionB;

        [FieldOffset(0x30)]
        public float RotationB;
    }

    private System.Numerics.Vector3? saveSitPosition;
    private float? saveSitRotation;

    public Plugin() {
        GameInteropProvider.InitializeFromAttributes(this);
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
        useEmote(new IntPtr(agent), 88, nint.Zero, 0, 0);
        suppressedSnap = false;
    }

    private void SitAnywhere(string command, string args) {
        var player = (Character*) (ClientState.LocalPlayer?.Address ?? nint.Zero);
        if (player == null) return;
        saveSitPosition = new System.Numerics.Vector3(player->GameObject.Position.X, player->GameObject.Position.Y, player->GameObject.Position.Z);
        saveSitRotation = player->GameObject.Rotation;
        var agent = AgentModule.Instance()->GetAgentByInternalId(AgentId.Emote);
        useEmote(new IntPtr(agent), 96, IntPtr.Zero, 0, 0);
    }
}
