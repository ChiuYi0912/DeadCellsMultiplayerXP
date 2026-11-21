namespace DeadCellsMultiplayerX.Client.Overrides.Weapons;

using dc.en;
using DeadCellsMultiplayerX.Engine.Overrides.Weapons;
using ModCore.Storage;


internal sealed class ClientWeaponsManager(Hero hero) : MultiplayerWeaponsManager(hero),
IHxbitSerializable<ClientWeaponsManager.IHxData>
{


    public sealed class IHxData { }
    IHxData IHxbitSerializable<IHxData>.GetData() => new();
    void IHxbitSerializable<IHxData>.SetData(IHxData data) { }
}
