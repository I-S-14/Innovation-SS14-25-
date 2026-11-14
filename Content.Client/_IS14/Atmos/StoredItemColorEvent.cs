namespace Content.Client._IS14.Atmos;

// IS14: equivalent to the Wizards #30645 storage-render tint hook.
public sealed class StoredItemColorEvent : EntityEventArgs
{
    public Color Color = Color.White;
}
