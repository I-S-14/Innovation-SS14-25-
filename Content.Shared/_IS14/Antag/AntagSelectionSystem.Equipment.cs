namespace Content.Shared.Antag;

public abstract partial class AntagSelectionSystem
{
    protected virtual void BeforeAntagEquip(EntityUid antag, AntagSpecifierPrototype prototype) { }
    protected virtual void AfterAntagEquip(EntityUid antag, AntagSpecifierPrototype prototype) { }
}
