public class X12Document
{
    public string InterchangeControlHeader { get; set; } = string.Empty;
    public string FunctionalGroupHeader { get; set; } = string.Empty;
    public string TransactionSetHeader { get; set; } = string.Empty;
    public string TransactionType { get; set; } = string.Empty;
    public char SegmentTerminator { get; set; }
    public char ElementSeparator { get; set; }
    public char ComponentSeparator { get; set; }
    public char RepetitionSeparator { get; set; }
    public List<X12Loop> Loops { get; set; } = new List<X12Loop>();
    public List<X12Segment> Segments { get; set; } = new List<X12Segment>();
    public string RawContent { get; set; } = string.Empty;
}

public class X12Loop
{
    public string LoopId { get; set; } = string.Empty;
    public List<X12Segment> Segments { get; set; } = new List<X12Segment>();
    public List<X12Loop> ChildLoops { get; set; } = new List<X12Loop>();
    public X12Loop? ParentLoop { get; set; }
    public int Depth { get; set; }
}

public class X12Segment
{
    public string Id { get; set; } = string.Empty;
    public List<string> Elements { get; set; } = new List<string>();
    public int Position { get; set; }
    public X12Loop? ParentLoop { get; set; }
}

public class ValidationResult
{
    public bool IsValid { get; set; }
    public List<ValidationError> Errors { get; set; } = new List<ValidationError>();
}

public class ValidationError
{
    public string SegmentId { get; set; } = string.Empty;
    public string LoopId { get; set; } = string.Empty;
    public string ErrorCode { get; set; } = string.Empty;
    public string ErrorDescription { get; set; } = string.Empty;
    public int Position { get; set; }
}