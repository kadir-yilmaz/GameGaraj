using GameGaraj.Catalog.API.Models;

namespace GameGaraj.Catalog.API.Repositories.Abstract
{
    /// <summary>
    /// Repository interface for CategoryAttribute data access operations
    /// </summary>
    public interface IAttributeRepository
    {
        /// <summary>
        /// Retrieves all attributes
        /// </summary>
        Task<List<CategoryAttribute>> GetAllAsync();

        /// <summary>
        /// Retrieves all attributes for a specific category
        /// </summary>
        /// <param name="categoryId">The category ID to filter by</param>
        /// <returns>List of attributes belonging to the category</returns>
        Task<List<CategoryAttribute>> GetByCategoryIdAsync(string categoryId);

        /// <summary>
        /// Retrieves a single attribute by its ID
        /// </summary>
        /// <param name="id">The attribute ID</param>
        /// <returns>The attribute if found, null otherwise</returns>
        Task<CategoryAttribute?> GetByIdAsync(string id);

        /// <summary>
        /// Creates a new attribute
        /// </summary>
        /// <param name="attribute">The attribute to create</param>
        /// <returns>The created attribute with generated ID</returns>
        Task<CategoryAttribute> CreateAsync(CategoryAttribute attribute);

        /// <summary>
        /// Updates an existing attribute
        /// </summary>
        /// <param name="id">The attribute ID to update</param>
        /// <param name="attribute">The updated attribute data</param>
        /// <returns>The updated attribute if found, null otherwise</returns>
        Task<CategoryAttribute?> UpdateAsync(string id, CategoryAttribute attribute);

        /// <summary>
        /// Deletes an attribute by its ID
        /// </summary>
        /// <param name="id">The attribute ID to delete</param>
        /// <returns>True if deleted successfully, false if not found</returns>
        Task<bool> DeleteAsync(string id);

        /// <summary>
        /// Deletes all attributes for a specific category (cascade delete)
        /// </summary>
        /// <param name="categoryId">The category ID whose attributes should be deleted</param>
        /// <returns>True if any attributes were deleted, false otherwise</returns>
        Task<bool> DeleteByCategoryIdAsync(string categoryId);

        /// <summary>
        /// Checks if an attribute with the given name already exists in the category
        /// </summary>
        /// <param name="categoryId">The category ID to check within</param>
        /// <param name="name">The attribute name to check</param>
        /// <returns>True if an attribute with this name exists in the category, false otherwise</returns>
        Task<bool> ExistsAsync(string categoryId, string name);
    }
}
