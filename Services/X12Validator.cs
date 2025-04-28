public class X12Validator : IX12Validator
{
    private readonly Dictionary<string, List<string>> _270LoopSegmentOrder;
    private readonly Dictionary<string, List<string>> _271LoopSegmentOrder;

    public X12Validator()
    {
        // Initialize segment order rules for 270 transaction
        _270LoopSegmentOrder = new Dictionary<string, List<string>>
        {
            // Header segments
            { "ROOT", new List<string> { "ISA", "GS", "ST", "BHT", "SE", "GE", "IEA" } },
            
            // Information Source Level
            { "HL_20", new List<string> { "HL", "NM1", "PER", "PRV" } },
            
            // Information Receiver Level
            { "HL_21", new List<string> { "HL", "NM1", "REF", "N3", "N4", "PER", "PRV" } },
            
            // Subscriber Level
            { "HL_22", new List<string> { "HL", "TRN", "NM1", "REF", "N3", "N4", "DMG", "DTP", "EQ" } },
            
            // Dependent Level
            { "HL_23", new List<string> { "HL", "TRN", "NM1", "REF", "N3", "N4", "DMG", "DTP", "EQ" } },
            
            // Provider Loop
            { "2100A", new List<string> { "NM1", "REF", "N3", "N4", "PER", "PRV" } },
            
            // Subscriber Loop
            { "2100B", new List<string> { "NM1", "REF", "N3", "N4", "DMG" } },
            
            // Dependent Loop
            { "2100C", new List<string> { "NM1", "REF", "N3", "N4", "DMG" } }
        };

        // Initialize segment order rules for 271 transaction
        _271LoopSegmentOrder = new Dictionary<string, List<string>>
        {
            // Header segments
            { "ROOT", new List<string> { "ISA", "GS", "ST", "BHT", "SE", "GE", "IEA" } },
            
            // Information Source Level
            { "HL_20", new List<string> { "HL", "AAA", "NM1", "PER", "PRV" } },
            
            // Information Receiver Level
            { "HL_21", new List<string> { "HL", "AAA", "NM1", "REF", "N3", "N4", "PER", "PRV" } },
            
            // Subscriber Level
            { "HL_22", new List<string> { "HL", "TRN", "AAA", "NM1", "REF", "N3", "N4", "DMG", "DTP", "EB", "HSD", "MSG" } },
            
            // Dependent Level
            { "HL_23", new List<string> { "HL", "TRN", "AAA", "NM1", "REF", "N3", "N4", "DMG", "DTP", "EB", "HSD", "MSG" } },
            
            // Payer Loop
            { "2100A", new List<string> { "NM1", "REF", "N3", "N4", "PER", "PRV", "AAA" } },
            
            // Information Receiver Loop
            { "2100B", new List<string> { "NM1", "REF", "N3", "N4", "AAA" } },
            
            // Subscriber Loop
            { "2100C", new List<string> { "NM1", "REF", "N3", "N4", "AAA", "DMG", "DTP", "EB" } },
            
            // Dependent Loop
            { "2100D", new List<string> { "NM1", "REF", "N3", "N4", "AAA", "DMG", "DTP", "EB" } }
        };
    }

    public ValidationResult Validate(X12Document document)
    {
        var result = new ValidationResult { IsValid = true };

        // Validate transaction type
        if (document.TransactionType != "270" && document.TransactionType != "271")
        {
            result.IsValid = false;
            result.Errors.Add(new ValidationError
            {
                SegmentId = "ST",
                ErrorCode = "3",
                ErrorDescription = $"Invalid transaction set identifier: {document.TransactionType}. Expected 270 or 271.",
                Position = document.Segments.FirstOrDefault(s => s.Id == "ST")?.Position ?? 0
            });
            return result; // Early return for invalid transaction type
        }

        // Validate envelope structure
        if (!ValidateEnvelopeStructure(document, result))
        {
            return result; // Early return for envelope structure issues
        }

        // Validate segment order in each loop
        ValidateLoopSegmentOrder(document, document.Loops[0], result);

        return result;
    }

    private bool ValidateEnvelopeStructure(X12Document document, ValidationResult result)
    {
        // Check for required envelope segments
        var requiredSegments = new[] { "ISA", "GS", "ST", "SE", "GE", "IEA" };
        foreach (var segmentId in requiredSegments)
        {
            if (!document.Segments.Any(s => s.Id == segmentId))
            {
                result.IsValid = false;
                result.Errors.Add(new ValidationError
                {
                    SegmentId = segmentId,
                    ErrorCode = "1",
                    ErrorDescription = $"Required segment {segmentId} is missing",
                    Position = 0
                });
                return false;
            }
        }

        // Validate ISA segment
        var isaSegment = document.Segments.First(s => s.Id == "ISA");
        if (isaSegment.Elements.Count < 16)
        {
            result.IsValid = false;
            result.Errors.Add(new ValidationError
            {
                SegmentId = "ISA",
                ErrorCode = "1",
                ErrorDescription = "ISA segment has insufficient elements",
                Position = isaSegment.Position
            });
            return false;
        }

        // Validate GS segment
        var gsSegment = document.Segments.First(s => s.Id == "GS");
        if (gsSegment.Elements.Count < 8)
        {
            result.IsValid = false;
            result.Errors.Add(new ValidationError
            {
                SegmentId = "GS",
                ErrorCode = "1",
                ErrorDescription = "GS segment has insufficient elements",
                Position = gsSegment.Position
            });
            return false;
        }

        // Validate ST segment
        var stSegment = document.Segments.First(s => s.Id == "ST");
        if (stSegment.Elements.Count < 2)
        {
            result.IsValid = false;
            result.Errors.Add(new ValidationError
            {
                SegmentId = "ST",
                ErrorCode = "1",
                ErrorDescription = "ST segment has insufficient elements",
                Position = stSegment.Position
            });
            return false;
        }

        // Validate transaction set identifier
        if (stSegment.Elements[0] != "270" && stSegment.Elements[0] != "271")
        {
            result.IsValid = false;
            result.Errors.Add(new ValidationError
            {
                SegmentId = "ST",
                ErrorCode = "3",
                ErrorDescription = $"Invalid transaction set identifier: {stSegment.Elements[0]}. Expected 270 or 271.",
                Position = stSegment.Position
            });
            return false;
        }

        return true;
    }

    private void ValidateLoopSegmentOrder(X12Document document, X12Loop loop, ValidationResult result)
    {
        // Get the appropriate segment order rules based on transaction type
        var segmentOrderRules = document.TransactionType == "270" ? _270LoopSegmentOrder : _271LoopSegmentOrder;

        // Check if we have rules for this loop
        if (segmentOrderRules.TryGetValue(loop.LoopId, out var expectedOrder))
        {
            // Track the last seen segment index in the expected order
            int lastSeenIndex = -1;

            foreach (var segment in loop.Segments)
            {
                int currentIndex = expectedOrder.IndexOf(segment.Id);
                
                // If segment is not in the expected list
                if (currentIndex == -1)
                {
                    result.IsValid = false;
                    result.Errors.Add(new ValidationError
                    {
                        SegmentId = segment.Id,
                        LoopId = loop.LoopId,
                        ErrorCode = "2",
                        ErrorDescription = $"Segment {segment.Id} is not allowed in loop {loop.LoopId}",
                        Position = segment.Position
                    });
                    return; // Stop validation on first error
                }
                
                // If segment is out of order
                if (currentIndex < lastSeenIndex)
                {
                    result.IsValid = false;
                    result.Errors.Add(new ValidationError
                    {
                        SegmentId = segment.Id,
                        LoopId = loop.LoopId,
                        ErrorCode = "3",
                        ErrorDescription = $"Segment {segment.Id} is out of sequence in loop {loop.LoopId}",
                        Position = segment.Position
                    });
                    return; // Stop validation on first error
                }
                
                lastSeenIndex = currentIndex;
            }
        }

        // Recursively validate child loops
        foreach (var childLoop in loop.ChildLoops)
        {
            ValidateLoopSegmentOrder(document, childLoop, result);
            
            // Stop validation on first error
            if (!result.IsValid)
                return;
        }
    }
}