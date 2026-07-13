namespace LibKdbx;

public class AutoTypeAssociation
{
    public string Window { get; set; } = "";
    public string Sequence { get; set; } = "";
}

public class AutoType
{
    public bool Enabled { get; set; } = true;
    public int DataTransferObfuscation { get; set; }
    public string DefaultSequence { get; set; } = "";
    public List<AutoTypeAssociation> Associations { get; set; } = [];

    public AutoType Clone() =>
        new()
        {
            Enabled = Enabled,
            DataTransferObfuscation = DataTransferObfuscation,
            DefaultSequence = DefaultSequence,
            Associations =
            [
                .. Associations.Select(a => new AutoTypeAssociation
                {
                    Window = a.Window,
                    Sequence = a.Sequence,
                }),
            ],
        };
}
