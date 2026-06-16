namespace GameGaraj.Catalog.API.Exceptions
{
    public class CatalogValidationException : Exception
    {
        public IReadOnlyList<string> Errors { get; }

        public CatalogValidationException(IEnumerable<string> errors)
            : base(string.Join(" ", errors))
        {
            Errors = errors.Where(error => !string.IsNullOrWhiteSpace(error)).ToList();
        }

        public CatalogValidationException(string error)
            : this(new[] { error })
        {
        }
    }
}
