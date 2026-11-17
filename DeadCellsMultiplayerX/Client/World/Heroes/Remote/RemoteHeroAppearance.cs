namespace DeadCellsMultiplayerX.Client.World.Heroes.Remote;

using dc;
using dc.libs.heaps.slib;
using DeadCellsMultiplayerX.Contracts.Snapshots;
using DeadCellsMultiplayerX.Engine.Serialization;
using Hashlink.Virtuals;
using ModCore.Utilities;

/// <summary>Applies network appearance data to the visible actor's sprite.</summary>
internal sealed class RemoteHeroAppearance(RemoteHero owner)
{
    private readonly RemoteHero owner = owner;
    private uint? appliedVersion;

    public void Invalidate() => this.appliedVersion = null;

    public bool Apply(HeroSpawn spawn)
    {
        if (!this.owner.IsActive || this.appliedVersion == spawn.Version || string.IsNullOrEmpty(spawn.ColorMapModel)
            || string.IsNullOrEmpty(spawn.ColorMapSkin) || spawn.MainSprite == null)
        {
            return false;
        }

        var library = Assets.Class.lib.get(spawn.MainSprite.AtlasName.AsHaxeString());
        if (library == null)
        {
            return false;
        }
        var group = spawn.MainSprite.GroupName.AsHaxeString();
        var normalMap = library.getNormalMapFromGroup(group);
        this.owner.initSprite(library, group, null, null, null, true, null, normalMap);
        if (this.owner.spr == null)
        {
            return false;
        }
        this.owner.spr.pivot.copyFrom(
            DCMXSerializers.MessagePack.Deserialize<SpritePivot>(spawn.MainSprite.PivotData));
        this.owner.setColorMap(spawn.ColorMapModel.AsHaxeString(), spawn.ColorMapSkin.AsHaxeString(), null);
        foreach (var (index, data) in spawn.GlowData)
        {
            if (data == null)
            {
                continue;
            }
            this.owner.setGlowData(index,
                DCMXSerializers.MessagePack.Deserialize<virtual_animationIntensity_animationScale_animationSpeed_animationTextureMask_inner_key_outer_power_>(data),
                this.owner.spr);
        }
        this.appliedVersion = spawn.Version;
        return true;
    }
}
