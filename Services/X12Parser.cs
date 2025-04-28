public class X12Parser : IX12Parser
{
    public X12Document Parse(string ediContent)
    {
        if (string.IsNullOrWhiteSpace(ediContent))
        {
            throw new ArgumentException("EDI content cannot be empty");
        }

        var document = new X12Document
        {
            RawContent = ediContent
        };

        // Extract delimiters from ISA segment
        if (ediContent.Length < 106 || !ediContent.StartsWith("ISA"))
        {
            throw new FormatException("Invalid EDI X12 format: ISA segment not found or incomplete");
        }

        document.ElementSeparator = ediContent[3];
        document.RepetitionSeparator = ediContent[82]; // ISA11
        document.ComponentSeparator = ediContent[104]; // ISA16
        
        // Find the segment terminator (should be after ISA16)
        document.SegmentTerminator = ediContent[105];

        // Split the document into segments
        var segments = SplitIntoSegments(ediContent, document.SegmentTerminator);
        
        // Process segments and build document structure
        ProcessSegments(segments, document);
        
        // Identify transaction type (270/271)
        IdentifyTransactionType(document);
        
        // Build loop structure
        BuildLoopStructure(document);

        return document;
    }

    private string[] SplitIntoSegments(string ediContent, char segmentTerminator)
    {
        return ediContent.Split(segmentTerminator)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToArray();
    }

    private void ProcessSegments(string[] segmentStrings, X12Document document)
    {
        for (int i = 0; i < segmentStrings.Length; i++)
        {
            var segmentString = segmentStrings[i].Trim();
            if (string.IsNullOrWhiteSpace(segmentString))
                continue;

            var elements = segmentString.Split(document.ElementSeparator);
            var segmentId = elements[0];

            var segment = new X12Segment
            {
                Id = segmentId,
                Elements = elements.Skip(1).ToList(),
                Position = i
            };

            document.Segments.Add(segment);

            // Store special segments
            if (segmentId == "ISA")
                document.InterchangeControlHeader = segmentString;
            else if (segmentId == "GS")
                document.FunctionalGroupHeader = segmentString;
            else if (segmentId == "ST")
                document.TransactionSetHeader = segmentString;
        }
    }

    private void IdentifyTransactionType(X12Document document)
    {
        var stSegment = document.Segments.FirstOrDefault(s => s.Id == "ST");
        if (stSegment != null && stSegment.Elements.Count > 0)
        {
            document.TransactionType = stSegment.Elements[0];
        }
    }

    private void BuildLoopStructure(X12Document document)
    {
        // Root loop (transaction set)
        var rootLoop = new X12Loop { LoopId = "ROOT", Depth = 0 };
        document.Loops.Add(rootLoop);

        // For 270/271 transactions, implement loop detection logic
        if (document.TransactionType == "270" || document.TransactionType == "271")
        {
            BuildHierarchicalLoops(document, rootLoop);
        }
    }

    private void BuildHierarchicalLoops(X12Document document, X12Loop rootLoop)
    {
        X12Loop currentLoop = rootLoop;
        X12Loop? currentHLoop = null;
        Dictionary<string, X12Loop> hLoopMap = new Dictionary<string, X12Loop>();

        foreach (var segment in document.Segments)
        {
            // Skip envelope segments
            if (segment.Id == "ISA" || segment.Id == "GS" || segment.Id == "ST" || 
                segment.Id == "SE" || segment.Id == "GE" || segment.Id == "IEA")
            {
                rootLoop.Segments.Add(segment);
                segment.ParentLoop = rootLoop;
                continue;
            }

            // Handle hierarchical loops (HL segments)
            if (segment.Id == "HL" && segment.Elements.Count >= 3)
            {
                string hlNumber = segment.Elements[0];
                string parentHlNumber = segment.Elements[1];
                string levelCode = segment.Elements[2];

                // Create a new loop for this HL
                var hLoop = new X12Loop
                {
                    LoopId = $"HL_{levelCode}",
                    Segments = new List<X12Segment> { segment },
                    Depth = string.IsNullOrEmpty(parentHlNumber) ? 1 : hLoopMap[parentHlNumber].Depth + 1
                };

                segment.ParentLoop = hLoop;
                
                // Set parent relationship
                if (!string.IsNullOrEmpty(parentHlNumber) && hLoopMap.ContainsKey(parentHlNumber))
                {
                    var parentLoop = hLoopMap[parentHlNumber];
                    hLoop.ParentLoop = parentLoop;
                    parentLoop.ChildLoops.Add(hLoop);
                }
                else
                {
                    rootLoop.ChildLoops.Add(hLoop);
                    hLoop.ParentLoop = rootLoop;
                }

                hLoopMap[hlNumber] = hLoop;
                currentHLoop = hLoop;
                currentLoop = hLoop;
            }
            else if (currentHLoop != null)
            {
                // Add segment to current loop
                currentLoop.Segments.Add(segment);
                segment.ParentLoop = currentLoop;

                // Check for loop start segments
                if (document.TransactionType == "270")
                {
                    // 270 specific loop detection
                    if (segment.Id == "NM1")
                    {
                        string entityIdentifier = segment.Elements.Count > 0 ? segment.Elements[0] : "";
                        DetectAndCreateLoop270(document, currentHLoop, segment, entityIdentifier);
                    }
                }
                else if (document.TransactionType == "271")
                {
                    // 271 specific loop detection
                    if (segment.Id == "NM1")
                    {
                        string entityIdentifier = segment.Elements.Count > 0 ? segment.Elements[0] : "";
                        DetectAndCreateLoop271(document, currentHLoop, segment, entityIdentifier);
                    }
                }
            }
            else
            {
                // Add to root if no current loop
                rootLoop.Segments.Add(segment);
                segment.ParentLoop = rootLoop;
            }
        }
    }

    private void DetectAndCreateLoop270(X12Document document, X12Loop parentLoop, X12Segment segment, string entityIdentifier)
    {
        // 270 specific loop detection logic based on NM1 entity identifier
        string loopId = "";
        
        switch (entityIdentifier)
        {
            case "1P": // Provider
                loopId = "2100A";
                break;
            case "IL": // Insured/Subscriber
                loopId = "2100B";
                break;
            case "03": // Dependent
                loopId = "2100C";
                break;
            default:
                return; // Not a recognized loop
        }

        CreateNestedLoop(parentLoop, segment, loopId);
    }

    private void DetectAndCreateLoop271(X12Document document, X12Loop parentLoop, X12Segment segment, string entityIdentifier)
    {
        // 271 specific loop detection logic based on NM1 entity identifier
        string loopId = "";
        
        switch (entityIdentifier)
        {
            case "PR": // Payer
                loopId = "2100A";
                break;
            case "1P": // Provider
                loopId = "2100B";
                break;
            case "IL": // Insured/Subscriber
                loopId = "2100C";
                break;
            case "03": // Dependent
                loopId = "2100D";
                break;
            default:
                return; // Not a recognized loop
        }

        CreateNestedLoop(parentLoop, segment, loopId);
    }

    private void CreateNestedLoop(X12Loop parentLoop, X12Segment segment, string loopId)
    {
        var newLoop = new X12Loop
        {
            LoopId = loopId,
            ParentLoop = parentLoop,
            Depth = parentLoop.Depth + 1,
            Segments = new List<X12Segment> { segment }
        };

        segment.ParentLoop = newLoop;
        parentLoop.ChildLoops.Add(newLoop);
    }
}