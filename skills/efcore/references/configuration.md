# EF Core Configuration

## Complete Entity Configuration

```csharp
public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        // Table
        builder.ToTable("orders", "sales");

        // Primary key
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        // Properties
        builder.Property(o => o.OrderNumber)
            .HasColumnName("order_number")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(o => o.CustomerId)
            .HasColumnName("customer_id")
            .IsRequired();

        // Enum as string
        builder.Property(o => o.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20);

        // Value object
        builder.OwnsOne(o => o.Total, money =>
        {
            money.Property(m => m.Amount)
                .HasColumnName("total_amount")
                .HasPrecision(18, 4);
            money.Property(m => m.Currency)
                .HasColumnName("total_currency")
                .HasMaxLength(3);
        });

        // Private collection
        builder.HasMany(o => o.Items)
            .WithOne()
            .HasForeignKey("order_id")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(o => o.Items)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // Relationship
        builder.HasOne(o => o.Customer)
            .WithMany()
            .HasForeignKey(o => o.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        // Audit columns
        builder.Property(o => o.CreatedAt)
            .HasColumnName("created_at");
        builder.Property(o => o.ModifiedAt)
            .HasColumnName("modified_at");

        // Concurrency token (PostgreSQL)
        builder.Property(o => o.Version)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .IsRowVersion();

        // Indexes
        builder.HasIndex(o => o.OrderNumber)
            .IsUnique()
            .HasDatabaseName("ix_orders_order_number");

        builder.HasIndex(o => o.CustomerId)
            .HasDatabaseName("ix_orders_customer_id");

        builder.HasIndex(o => new { o.TenantId, o.Status })
            .HasDatabaseName("ix_orders_tenant_status");
    }
}
```

## Registration

```csharp
services.AddDbContext<AppDbContext>((sp, options) =>
{
    options.UseNpgsql(connectionString, npgsql =>
    {
        npgsql.EnableRetryOnFailure(3);
        npgsql.CommandTimeout(30);
        npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
    });

    options.AddInterceptors(
        sp.GetRequiredService<AuditInterceptor>());

    if (env.IsDevelopment())
    {
        options.EnableDetailedErrors();
        options.EnableSensitiveDataLogging();
    }
});

services.AddScoped<IDbContext>(sp =>
    sp.GetRequiredService<AppDbContext>());
```

## Conventions

```csharp
protected override void ConfigureConventions(
    ModelConfigurationBuilder configurationBuilder)
{
    configurationBuilder.Properties<string>()
        .HaveMaxLength(500);

    configurationBuilder.Properties<decimal>()
        .HavePrecision(18, 4);

    configurationBuilder.Properties<DateTimeOffset>()
        .HaveColumnType("timestamp with time zone");
}
```
