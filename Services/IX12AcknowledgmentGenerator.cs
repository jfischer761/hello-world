public interface IX12AcknowledgmentGenerator
{
    string Generate(X12Document document, ValidationResult validationResult);
}