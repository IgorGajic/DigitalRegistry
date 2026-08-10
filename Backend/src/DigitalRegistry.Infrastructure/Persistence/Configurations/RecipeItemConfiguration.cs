using DigitalRegistry.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DigitalRegistry.Infrastructure.Persistence.Configurations;

public class RecipeItemConfiguration : IEntityTypeConfiguration<RecipeItem>
{
    public void Configure(EntityTypeBuilder<RecipeItem> builder)
    {
        builder.HasKey(recipeItem => recipeItem.Id);

        builder.Property(recipeItem => recipeItem.QuantityRequired)
            .HasPrecision(18, 3)
            .IsRequired();

        builder.HasOne(recipeItem => recipeItem.Ingredient)
            .WithMany(ingredient => ingredient.UsedIn)
            .HasForeignKey(recipeItem => recipeItem.IngredientId)
            // Refuse to delete an ingredient that recipes still depend on.
            .OnDelete(DeleteBehavior.Restrict);

        // An ingredient may appear only once per recipe; quantities are summed into one line.
        builder.HasIndex(recipeItem => new { recipeItem.MenuItemId, recipeItem.IngredientId })
            .IsUnique();

        builder.ToTable(table =>
            table.HasCheckConstraint("CK_RecipeItem_Quantity_Positive", "[QuantityRequired] > 0"));
    }
}
