using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Promoos.Entities;

namespace Promoos.Data.Configurations;

internal class TaskItemConfig : IEntityTypeConfiguration<TaskItem>
{
    public void Configure(EntityTypeBuilder<TaskItem> builder)
    {
        builder.ToTable(nameof(TaskItem));
        builder.Property(x => x.Title).HasMaxLength(200);
        builder.HasIndex(x => x.TaskId).HasMethod("hash");
    }
}
