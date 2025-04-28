public class X12AcknowledgmentGenerator : IX12AcknowledgmentGenerator
{
    public string Generate(X12Document document, ValidationResult validationResult)
    {
        if (validationResult.IsValid)
        {
            throw new InvalidOperationException("Cannot generate 999 acknowledgment for a valid document");
        }

        var sb = new StringBuilder();
        var controlNumber = DateTime.Now.ToString("yyyyMMddHHmmss");
        
        // Extract original values from the document
        var isaSegment = document.Segments.First(s => s.Id == "ISA");
        var gsSegment = document.Segments.First(s => s.Id == "GS");
        var stSegment = document.Segments.First(s => s.Id == "ST");
        
        // Get delimiters from the original document
        char elementSeparator = document.ElementSeparator;
        char segmentTerminator = document.SegmentTerminator;
        char componentSeparator = document.ComponentSeparator;
        
        // ISA - Interchange Control Header
        sb.Append("ISA");
        sb.Append(elementSeparator).Append("00").Append(elementSeparator).Append("          ");
        sb.Append(elementSeparator).Append("00").Append(elementSeparator).Append("          ");
        sb.Append(elementSeparator).Append(isaSegment.Elements[7]).Append(elementSeparator).Append(isaSegment.Elements[6]);
        sb.Append(elementSeparator).Append(isaSegment.Elements[5]).Append(elementSeparator).Append(isaSegment.Elements[4]);
        sb.Append(elementSeparator).Append(DateTime.Now.ToString("yyMMdd"));
        sb.Append(elementSeparator).Append(DateTime.Now.ToString("HHmm"));
        sb.Append(elementSeparator).Append("^");
        sb.Append(elementSeparator).Append("00501");
        sb.Append(elementSeparator).Append(controlNumber);
        sb.Append(elementSeparator).Append("0");
        sb.Append(elementSeparator).Append("P");
        sb.Append(elementSeparator).Append(">").Append(segmentTerminator);
        
        // GS - Functional Group Header
        sb.Append("GS");
        sb.Append(elementSeparator).Append("FA");
        sb.Append(elementSeparator).Append(gsSegment.Elements[1]);
        sb.Append(elementSeparator).Append(gsSegment.Elements[0]);
        sb.Append(elementSeparator).Append(DateTime.Now.ToString("yyyyMMdd"));
        sb.Append(elementSeparator).Append(DateTime.Now.ToString("HHmmss"));
        sb.Append(elementSeparator).Append(controlNumber);
        sb.Append(elementSeparator).Append("X");
        sb.Append(elementSeparator).Append("005010X231A1").Append(segmentTerminator);
        
        // ST - Transaction Set Header
        sb.Append("ST");
        sb.Append(elementSeparator).Append("999");
        sb.Append(elementSeparator).Append("0001");
        sb.Append(elementSeparator).Append("005010X231A1").Append(segmentTerminator);
        
        // AK1 - Functional Group Response Header
        sb.Append("AK1");
        sb.Append(elementSeparator).Append(gsSegment.Elements[0]);
        sb.Append(elementSeparator).Append(gsSegment.Elements[6]).Append(segmentTerminator);
        
        // AK2 - Transaction Set Response Header
        sb.Append("AK2");
        sb.Append(elementSeparator).Append(stSegment.Elements[0]);
        sb.Append(elementSeparator).Append(stSegment.Elements[1]).Append(segmentTerminator);
        
        // Generate IK3 segments for each error
        foreach (var error in validationResult.Errors)
        {
            // IK3 - Error Identification
            sb.Append("IK3");
            sb.Append(elementSeparator).Append(error.SegmentId);
            sb.Append(elementSeparator).Append(error.Position);
            sb.Append(elementSeparator).Append(error.ErrorCode);
            
            // Add error description as a note if available
            if (!string.IsNullOrEmpty(error.ErrorDescription))
            {
                sb.Append(segmentTerminator);
                sb.Append("CTX");
                sb.Append(elementSeparator).Append("SITUATIONAL TRIGGER");
                sb.Append(elementSeparator).Append(componentSeparator).Append("1");
                sb.Append(componentSeparator).Append(error.ErrorDescription);
            }
            
            sb.Append(segmentTerminator);
        }
        
        // AK5 - Transaction Set Response Trailer
        sb.Append("AK5");
        sb.Append(elementSeparator).Append("R"); // R = Rejected
        sb.Append(elementSeparator).Append("5"); // Error Code 5 = One or more segments in error
        sb.Append(segmentTerminator);
        
        // AK9 - Functional Group Response Trailer
        sb.Append("AK9");
        sb.Append(elementSeparator).Append("R"); // R = Rejected
        sb.Append(elementSeparator).Append("1"); // Number of transaction sets included
        sb.Append(elementSeparator).Append("1"); // Number of received transaction sets
        sb.Append(elementSeparator).Append("0"); // Number of accepted transaction sets
        sb.Append(elementSeparator).Append("5"); // Error Code 5 = One or more transaction sets were rejected
        sb.Append(segmentTerminator);
        
        // SE - Transaction Set Trailer
        int segmentCount = 7 + validationResult.Errors.Count * 2; // Base segments + error segments
        sb.Append("SE");
        sb.Append(elementSeparator).Append(segmentCount);
        sb.Append(elementSeparator).Append("0001").Append(segmentTerminator);
        
        // GE - Functional Group Trailer
        sb.Append("GE");
        sb.Append(elementSeparator).Append("1");
        sb.Append(elementSeparator).Append(controlNumber).Append(segmentTerminator);
        
        // IEA - Interchange Control Trailer
        sb.Append("IEA");
        sb.Append(elementSeparator).Append("1");
        sb.Append(elementSeparator).Append(controlNumber).Append(segmentTerminator);
        
        return sb.ToString();
    }
}