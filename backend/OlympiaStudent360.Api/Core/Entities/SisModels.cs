namespace OlympiaStudent360.Api.Core.Entities;

/// <summary>Latest source document. Nested memberships are replaced with every upsert.</summary>
public sealed class SisWarehouseRecord : BaseEntity
{
    public string SourceKey { get; set; } = default!;
    public string Endpoint { get; set; } = default!;
    public string RecordKey { get; set; } = default!;
    public string SourceId { get; set; } = default!;
    public string? SchoolId { get; set; }
    public string? SchoolYearId { get; set; }
    public string? StudentUserId { get; set; }
    public string? RecordType { get; set; }
    public DateTime? RecordDate { get; set; }
    public bool IsDeleted { get; set; }
    public bool? IsActive { get; set; }
    public DateTime SourceUpdatedAtUtc { get; set; }
    public string PayloadJson { get; set; } = default!;
    public string LastSeenScanId { get; set; } = default!;
}

/// <summary>One server-issued watermark per client, endpoint and exact filter set.</summary>
public sealed class SisSyncState : BaseEntity
{
    public string SourceKey { get; set; } = default!;
    public string Endpoint { get; set; } = default!;
    public string FilterKey { get; set; } = default!;
    public string FiltersJson { get; set; } = default!;
    public string? LastSuccessfulWatermark { get; set; }
    public bool ProjectsCurrentRoster { get; set; }
    public DateTime? LastCompletedAtUtc { get; set; }
}

public sealed class SisSyncRun : BaseEntity
{
    public string SourceKey { get; set; } = default!;
    public string Mode { get; set; } = default!;
    public string Status { get; set; } = default!;
    public DateTime StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public int Records { get; set; }
    public int Scans { get; set; }
    public string? Error { get; set; }
}
