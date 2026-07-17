using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace OlympiaStudent360.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "AcademicPeriods",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    AcademicYear = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    Term = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    IsCurrent = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AcademicPeriods", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(255)", nullable: false),
                    Name = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "longtext", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(255)", nullable: false),
                    FullName = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LastLoginAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UserName = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    PasswordHash = table.Column<string>(type: "longtext", nullable: true),
                    SecurityStamp = table.Column<string>(type: "longtext", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "longtext", nullable: true),
                    PhoneNumber = table.Column<string>(type: "longtext", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "datetime", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "CanvasCourses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    CanvasCourseId = table.Column<long>(type: "bigint", nullable: false),
                    SisSourceId = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true),
                    Name = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    CourseCode = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    WorkflowState = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    EnrollmentTermId = table.Column<long>(type: "bigint", nullable: true),
                    TermName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    Subject = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    AcademicYear = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true),
                    Term = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CanvasCourses", x => x.Id);
                    table.UniqueConstraint("AK_CanvasCourses_CanvasCourseId", x => x.CanvasCourseId);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "CustomReports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    OwnerUserId = table.Column<string>(type: "varchar(450)", maxLength: 450, nullable: false),
                    OwnerUsername = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false),
                    Title = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false),
                    DefinitionJson = table.Column<string>(type: "longtext", nullable: false),
                    SharedWithJson = table.Column<string>(type: "longtext", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomReports", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "DashboardMetrics",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    Key = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    Label = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    Domain = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    Source = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    Unit = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DashboardMetrics", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "DashboardPreferences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    UserId = table.Column<string>(type: "varchar(450)", maxLength: 450, nullable: false),
                    AcademicYear = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true),
                    Term = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    MeasurementCycle = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    Campus = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    EducationLevel = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    GradeLevel = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    Homeroom = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    StudentProfileId = table.Column<int>(type: "int", nullable: true),
                    Program = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    Subject = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DashboardPreferences", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ImportHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    ModuleId = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    FileName = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    Status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    ValidRows = table.Column<int>(type: "int", nullable: false),
                    InvalidRows = table.Column<int>(type: "int", nullable: false),
                    ImportedByUserId = table.Column<string>(type: "varchar(450)", maxLength: 450, nullable: false),
                    ImportedByUsername = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false),
                    ImportedByName = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportHistories", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ImportModuleFields",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    ModuleId = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    FieldKey = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    Label = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false),
                    FieldType = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    Required = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    OptionsJson = table.Column<string>(type: "longtext", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportModuleFields", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "SISClasses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    SISClassCode = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    ClassName = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    AcademicYear = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    Term = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    GradeLevel = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    Homeroom = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    Campus = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SISClasses", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "StudentClassificationSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    RiskWindowDays = table.Column<int>(type: "int", nullable: false),
                    HighRiskBehaviourCount = table.Column<int>(type: "int", nullable: false),
                    HighRiskBusViolationCount = table.Column<int>(type: "int", nullable: false),
                    HighRiskCanvasAverageBelow = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    CommendationWindowDays = table.Column<int>(type: "int", nullable: false),
                    CommendationPositiveBehaviourCount = table.Column<int>(type: "int", nullable: false),
                    CommendationCanvasAverageAtLeast = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    CommendationMaxBusViolations = table.Column<int>(type: "int", nullable: false),
                    UpdatedByUserId = table.Column<string>(type: "varchar(450)", maxLength: 450, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentClassificationSettings", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "StudentProfiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    StudentCode = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    FullName = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    GradeLevel = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    Homeroom = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    Campus = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    EducationLevel = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    Program = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentProfiles", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    RoleId = table.Column<string>(type: "varchar(255)", nullable: false),
                    ClaimType = table.Column<string>(type: "longtext", nullable: true),
                    ClaimValue = table.Column<string>(type: "longtext", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    UserId = table.Column<string>(type: "varchar(255)", nullable: false),
                    ClaimType = table.Column<string>(type: "longtext", nullable: true),
                    ClaimValue = table.Column<string>(type: "longtext", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "varchar(255)", nullable: false),
                    ProviderKey = table.Column<string>(type: "varchar(255)", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "longtext", nullable: true),
                    UserId = table.Column<string>(type: "varchar(255)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "varchar(255)", nullable: false),
                    RoleId = table.Column<string>(type: "varchar(255)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "varchar(255)", nullable: false),
                    LoginProvider = table.Column<string>(type: "varchar(255)", nullable: false),
                    Name = table.Column<string>(type: "varchar(255)", nullable: false),
                    Value = table.Column<string>(type: "longtext", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "CanvasAssignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    CanvasAssignmentId = table.Column<long>(type: "bigint", nullable: false),
                    CanvasCourseId = table.Column<long>(type: "bigint", nullable: false),
                    Title = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false),
                    PointsPossible = table.Column<double>(type: "double", nullable: true),
                    DueAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    WorkflowState = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    AssignmentGroupName = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true),
                    GradingType = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CanvasAssignments", x => x.Id);
                    table.UniqueConstraint("AK_CanvasAssignments_CanvasAssignmentId", x => x.CanvasAssignmentId);
                    table.ForeignKey(
                        name: "FK_CanvasAssignments_CanvasCourses_CanvasCourseId",
                        column: x => x.CanvasCourseId,
                        principalTable: "CanvasCourses",
                        principalColumn: "CanvasCourseId",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "WorkspacePreferences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    CustomReportId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<string>(type: "varchar(450)", maxLength: 450, nullable: false),
                    AcademicYear = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true),
                    Term = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    MeasurementCycle = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    Campus = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    EducationLevel = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    GradeLevel = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    Homeroom = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    StudentProfileId = table.Column<int>(type: "int", nullable: true),
                    Program = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    Subject = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    ChartOrderJson = table.Column<string>(type: "longtext", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkspacePreferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkspacePreferences_CustomReports_CustomReportId",
                        column: x => x.CustomReportId,
                        principalTable: "CustomReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "SISClassTeachers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    SISClassId = table.Column<int>(type: "int", nullable: false),
                    TeacherUserId = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    AssignmentRole = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SISClassTeachers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SISClassTeachers_AspNetUsers_TeacherUserId",
                        column: x => x.TeacherUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SISClassTeachers_SISClasses_SISClassId",
                        column: x => x.SISClassId,
                        principalTable: "SISClasses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "CanvasEnrollments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    CanvasEnrollmentId = table.Column<long>(type: "bigint", nullable: false),
                    CanvasUserId = table.Column<long>(type: "bigint", nullable: false),
                    CanvasCourseId = table.Column<long>(type: "bigint", nullable: false),
                    Type = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    WorkflowState = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    SisUserId = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    StudentProfileId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CanvasEnrollments", x => x.Id);
                    table.UniqueConstraint("AK_CanvasEnrollments_CanvasEnrollmentId", x => x.CanvasEnrollmentId);
                    table.ForeignKey(
                        name: "FK_CanvasEnrollments_CanvasCourses_CanvasCourseId",
                        column: x => x.CanvasCourseId,
                        principalTable: "CanvasCourses",
                        principalColumn: "CanvasCourseId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CanvasEnrollments_StudentProfiles_StudentProfileId",
                        column: x => x.StudentProfileId,
                        principalTable: "StudentProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "DashboardMetricObservations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    DashboardMetricId = table.Column<int>(type: "int", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Value = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    BaselineValue = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    KpiTarget = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    AcademicYear = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true),
                    Term = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    MeasurementCycle = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    Campus = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    EducationLevel = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    GradeLevel = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    Homeroom = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    StudentProfileId = table.Column<int>(type: "int", nullable: true),
                    Program = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    Subject = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DashboardMetricObservations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DashboardMetricObservations_DashboardMetrics_DashboardMetric~",
                        column: x => x.DashboardMetricId,
                        principalTable: "DashboardMetrics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DashboardMetricObservations_StudentProfiles_StudentProfileId",
                        column: x => x.StudentProfileId,
                        principalTable: "StudentProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "SISClassStudents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    SISClassId = table.Column<int>(type: "int", nullable: false),
                    StudentProfileId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SISClassStudents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SISClassStudents_SISClasses_SISClassId",
                        column: x => x.SISClassId,
                        principalTable: "SISClasses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SISClassStudents_StudentProfiles_StudentProfileId",
                        column: x => x.StudentProfileId,
                        principalTable: "StudentProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "SISHealthVisits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    StudentProfileId = table.Column<int>(type: "int", nullable: true),
                    ExamTime = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    FullName = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    Gender = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true),
                    ClassName = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    Symptoms = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true),
                    Diagnosis = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true),
                    Classification = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    Treatment = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true),
                    Medication = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    ExaminedBy = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SISHealthVisits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SISHealthVisits_StudentProfiles_StudentProfileId",
                        column: x => x.StudentProfileId,
                        principalTable: "StudentProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "SISStudentParents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    StudentProfileId = table.Column<int>(type: "int", nullable: false),
                    FullName = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    Relationship = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    PhoneNumber = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    Email = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true),
                    AgeGroup = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    Occupation = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true),
                    EmploymentSector = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SISStudentParents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SISStudentParents_StudentProfiles_StudentProfileId",
                        column: x => x.StudentProfileId,
                        principalTable: "StudentProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "StudentBehaviours",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    ImportHistoryId = table.Column<int>(type: "int", nullable: true),
                    StudentProfileId = table.Column<int>(type: "int", nullable: false),
                    IncidentDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    BehaviourType = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    Notes = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true),
                    RecordedByName = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true),
                    AcademicYear = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true),
                    Term = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    MeasurementCycle = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    Program = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    Subject = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    CustomFieldsJson = table.Column<string>(type: "longtext", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentBehaviours", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentBehaviours_ImportHistories_ImportHistoryId",
                        column: x => x.ImportHistoryId,
                        principalTable: "ImportHistories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StudentBehaviours_StudentProfiles_StudentProfileId",
                        column: x => x.StudentProfileId,
                        principalTable: "StudentProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "StudentBusAttendanceRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    ImportHistoryId = table.Column<int>(type: "int", nullable: true),
                    StudentProfileId = table.Column<int>(type: "int", nullable: false),
                    AttendanceDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    TripDirection = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    ViolationType = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    BusCode = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    RouteName = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true),
                    Notes = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true),
                    RecordedByName = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true),
                    AcademicYear = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true),
                    Term = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    CustomFieldsJson = table.Column<string>(type: "longtext", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentBusAttendanceRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentBusAttendanceRecords_ImportHistories_ImportHistoryId",
                        column: x => x.ImportHistoryId,
                        principalTable: "ImportHistories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StudentBusAttendanceRecords_StudentProfiles_StudentProfileId",
                        column: x => x.StudentProfileId,
                        principalTable: "StudentProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "StudentMentalHealthRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    ImportHistoryId = table.Column<int>(type: "int", nullable: true),
                    StudentProfileId = table.Column<int>(type: "int", nullable: false),
                    RecordDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    AcademicYear = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    ConcernType = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    RiskLevel = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    Notes = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true),
                    RecordedByUserId = table.Column<string>(type: "varchar(450)", maxLength: 450, nullable: false),
                    RecordedByName = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    CustomFieldsJson = table.Column<string>(type: "longtext", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentMentalHealthRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentMentalHealthRecords_ImportHistories_ImportHistoryId",
                        column: x => x.ImportHistoryId,
                        principalTable: "ImportHistories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StudentMentalHealthRecords_StudentProfiles_StudentProfileId",
                        column: x => x.StudentProfileId,
                        principalTable: "StudentProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "StudentProjects",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    ImportHistoryId = table.Column<int>(type: "int", nullable: true),
                    StudentProfileId = table.Column<int>(type: "int", nullable: false),
                    ProjectName = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    Subject = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    TeacherNotes = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true),
                    AcademicYear = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true),
                    Term = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    MeasurementCycle = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    Program = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    ActivityDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CustomFieldsJson = table.Column<string>(type: "longtext", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentProjects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentProjects_ImportHistories_ImportHistoryId",
                        column: x => x.ImportHistoryId,
                        principalTable: "ImportHistories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StudentProjects_StudentProfiles_StudentProfileId",
                        column: x => x.StudentProfileId,
                        principalTable: "StudentProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "CanvasSubmissions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    CanvasSubmissionId = table.Column<long>(type: "bigint", nullable: false),
                    CanvasAssignmentId = table.Column<long>(type: "bigint", nullable: false),
                    CanvasCourseId = table.Column<long>(type: "bigint", nullable: false),
                    CanvasUserId = table.Column<long>(type: "bigint", nullable: false),
                    SisUserId = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    StudentProfileId = table.Column<int>(type: "int", nullable: true),
                    Score = table.Column<double>(type: "double", nullable: true),
                    Grade = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    PointsPossible = table.Column<double>(type: "double", nullable: true),
                    WorkflowState = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    SubmissionType = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    GradedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DueAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Late = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Missing = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Excused = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    SecondsLate = table.Column<long>(type: "bigint", nullable: true),
                    Subject = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    AcademicYear = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true),
                    Term = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CanvasSubmissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CanvasSubmissions_CanvasAssignments_CanvasAssignmentId",
                        column: x => x.CanvasAssignmentId,
                        principalTable: "CanvasAssignments",
                        principalColumn: "CanvasAssignmentId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CanvasSubmissions_StudentProfiles_StudentProfileId",
                        column: x => x.StudentProfileId,
                        principalTable: "StudentProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "CanvasCourseScores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    CanvasScoreId = table.Column<long>(type: "bigint", nullable: false),
                    CanvasEnrollmentId = table.Column<long>(type: "bigint", nullable: false),
                    StudentProfileId = table.Column<int>(type: "int", nullable: true),
                    CourseScore = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    GradingPeriodId = table.Column<long>(type: "bigint", nullable: true),
                    GradingPeriodTitle = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    CurrentScore = table.Column<double>(type: "double", nullable: true),
                    FinalScore = table.Column<double>(type: "double", nullable: true),
                    OverrideScore = table.Column<double>(type: "double", nullable: true),
                    CourseName = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true),
                    Subject = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    AcademicYear = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true),
                    Term = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CanvasCourseScores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CanvasCourseScores_CanvasEnrollments_CanvasEnrollmentId",
                        column: x => x.CanvasEnrollmentId,
                        principalTable: "CanvasEnrollments",
                        principalColumn: "CanvasEnrollmentId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CanvasCourseScores_StudentProfiles_StudentProfileId",
                        column: x => x.StudentProfileId,
                        principalTable: "StudentProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_AcademicPeriods_AcademicYear_Term",
                table: "AcademicPeriods",
                columns: new[] { "AcademicYear", "Term" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AcademicPeriods_IsCurrent",
                table: "AcademicPeriods",
                column: "IsCurrent");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CanvasAssignments_CanvasAssignmentId",
                table: "CanvasAssignments",
                column: "CanvasAssignmentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CanvasAssignments_CanvasCourseId",
                table: "CanvasAssignments",
                column: "CanvasCourseId");

            migrationBuilder.CreateIndex(
                name: "IX_CanvasCourses_CanvasCourseId",
                table: "CanvasCourses",
                column: "CanvasCourseId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CanvasCourses_EnrollmentTermId",
                table: "CanvasCourses",
                column: "EnrollmentTermId");

            migrationBuilder.CreateIndex(
                name: "IX_CanvasCourseScores_AcademicYear_Term",
                table: "CanvasCourseScores",
                columns: new[] { "AcademicYear", "Term" });

            migrationBuilder.CreateIndex(
                name: "IX_CanvasCourseScores_CanvasEnrollmentId",
                table: "CanvasCourseScores",
                column: "CanvasEnrollmentId");

            migrationBuilder.CreateIndex(
                name: "IX_CanvasCourseScores_CanvasScoreId",
                table: "CanvasCourseScores",
                column: "CanvasScoreId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CanvasCourseScores_StudentProfileId",
                table: "CanvasCourseScores",
                column: "StudentProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_CanvasEnrollments_CanvasCourseId",
                table: "CanvasEnrollments",
                column: "CanvasCourseId");

            migrationBuilder.CreateIndex(
                name: "IX_CanvasEnrollments_CanvasEnrollmentId",
                table: "CanvasEnrollments",
                column: "CanvasEnrollmentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CanvasEnrollments_CanvasUserId",
                table: "CanvasEnrollments",
                column: "CanvasUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CanvasEnrollments_StudentProfileId",
                table: "CanvasEnrollments",
                column: "StudentProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_CanvasSubmissions_AcademicYear_Term",
                table: "CanvasSubmissions",
                columns: new[] { "AcademicYear", "Term" });

            migrationBuilder.CreateIndex(
                name: "IX_CanvasSubmissions_CanvasAssignmentId",
                table: "CanvasSubmissions",
                column: "CanvasAssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_CanvasSubmissions_CanvasSubmissionId",
                table: "CanvasSubmissions",
                column: "CanvasSubmissionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CanvasSubmissions_StudentProfileId",
                table: "CanvasSubmissions",
                column: "StudentProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomReports_OwnerUserId",
                table: "CustomReports",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DashboardMetricObservations_AcademicYear_Term_MeasurementCyc~",
                table: "DashboardMetricObservations",
                columns: new[] { "AcademicYear", "Term", "MeasurementCycle" });

            migrationBuilder.CreateIndex(
                name: "IX_DashboardMetricObservations_DashboardMetricId_PeriodStart",
                table: "DashboardMetricObservations",
                columns: new[] { "DashboardMetricId", "PeriodStart" });

            migrationBuilder.CreateIndex(
                name: "IX_DashboardMetricObservations_StudentProfileId",
                table: "DashboardMetricObservations",
                column: "StudentProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_DashboardMetrics_Key",
                table: "DashboardMetrics",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DashboardPreferences_UserId",
                table: "DashboardPreferences",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ImportHistories_CreatedAt",
                table: "ImportHistories",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ImportHistories_ImportedByUserId",
                table: "ImportHistories",
                column: "ImportedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ImportHistories_ModuleId_CreatedAt",
                table: "ImportHistories",
                columns: new[] { "ModuleId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ImportModuleFields_ModuleId",
                table: "ImportModuleFields",
                column: "ModuleId");

            migrationBuilder.CreateIndex(
                name: "IX_ImportModuleFields_ModuleId_FieldKey",
                table: "ImportModuleFields",
                columns: new[] { "ModuleId", "FieldKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SISClasses_AcademicYear_IsActive",
                table: "SISClasses",
                columns: new[] { "AcademicYear", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_SISClasses_SISClassCode",
                table: "SISClasses",
                column: "SISClassCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SISClassStudents_SISClassId_StudentProfileId",
                table: "SISClassStudents",
                columns: new[] { "SISClassId", "StudentProfileId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SISClassStudents_StudentProfileId",
                table: "SISClassStudents",
                column: "StudentProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_SISClassTeachers_SISClassId_TeacherUserId",
                table: "SISClassTeachers",
                columns: new[] { "SISClassId", "TeacherUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SISClassTeachers_TeacherUserId",
                table: "SISClassTeachers",
                column: "TeacherUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SISHealthVisits_ExamTime",
                table: "SISHealthVisits",
                column: "ExamTime");

            migrationBuilder.CreateIndex(
                name: "IX_SISHealthVisits_StudentProfileId",
                table: "SISHealthVisits",
                column: "StudentProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_SISStudentParents_StudentProfileId",
                table: "SISStudentParents",
                column: "StudentProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentBehaviours_ImportHistoryId",
                table: "StudentBehaviours",
                column: "ImportHistoryId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentBehaviours_IncidentDate",
                table: "StudentBehaviours",
                column: "IncidentDate");

            migrationBuilder.CreateIndex(
                name: "IX_StudentBehaviours_StudentProfileId",
                table: "StudentBehaviours",
                column: "StudentProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentBusAttendanceRecords_AttendanceDate",
                table: "StudentBusAttendanceRecords",
                column: "AttendanceDate");

            migrationBuilder.CreateIndex(
                name: "IX_StudentBusAttendanceRecords_ImportHistoryId",
                table: "StudentBusAttendanceRecords",
                column: "ImportHistoryId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentBusAttendanceRecords_StudentProfileId",
                table: "StudentBusAttendanceRecords",
                column: "StudentProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentBusAttendanceRecords_ViolationType_TripDirection_Atte~",
                table: "StudentBusAttendanceRecords",
                columns: new[] { "ViolationType", "TripDirection", "AttendanceDate" });

            migrationBuilder.CreateIndex(
                name: "IX_StudentMentalHealthRecords_AcademicYear_RiskLevel",
                table: "StudentMentalHealthRecords",
                columns: new[] { "AcademicYear", "RiskLevel" });

            migrationBuilder.CreateIndex(
                name: "IX_StudentMentalHealthRecords_ImportHistoryId",
                table: "StudentMentalHealthRecords",
                column: "ImportHistoryId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentMentalHealthRecords_RecordDate",
                table: "StudentMentalHealthRecords",
                column: "RecordDate");

            migrationBuilder.CreateIndex(
                name: "IX_StudentMentalHealthRecords_StudentProfileId",
                table: "StudentMentalHealthRecords",
                column: "StudentProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentProfiles_StudentCode",
                table: "StudentProfiles",
                column: "StudentCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudentProjects_ImportHistoryId",
                table: "StudentProjects",
                column: "ImportHistoryId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentProjects_StudentProfileId",
                table: "StudentProjects",
                column: "StudentProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkspacePreferences_CustomReportId_UserId",
                table: "WorkspacePreferences",
                columns: new[] { "CustomReportId", "UserId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AcademicPeriods");

            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "CanvasCourseScores");

            migrationBuilder.DropTable(
                name: "CanvasSubmissions");

            migrationBuilder.DropTable(
                name: "DashboardMetricObservations");

            migrationBuilder.DropTable(
                name: "DashboardPreferences");

            migrationBuilder.DropTable(
                name: "ImportModuleFields");

            migrationBuilder.DropTable(
                name: "SISClassStudents");

            migrationBuilder.DropTable(
                name: "SISClassTeachers");

            migrationBuilder.DropTable(
                name: "SISHealthVisits");

            migrationBuilder.DropTable(
                name: "SISStudentParents");

            migrationBuilder.DropTable(
                name: "StudentBehaviours");

            migrationBuilder.DropTable(
                name: "StudentBusAttendanceRecords");

            migrationBuilder.DropTable(
                name: "StudentClassificationSettings");

            migrationBuilder.DropTable(
                name: "StudentMentalHealthRecords");

            migrationBuilder.DropTable(
                name: "StudentProjects");

            migrationBuilder.DropTable(
                name: "WorkspacePreferences");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "CanvasEnrollments");

            migrationBuilder.DropTable(
                name: "CanvasAssignments");

            migrationBuilder.DropTable(
                name: "DashboardMetrics");

            migrationBuilder.DropTable(
                name: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "SISClasses");

            migrationBuilder.DropTable(
                name: "ImportHistories");

            migrationBuilder.DropTable(
                name: "CustomReports");

            migrationBuilder.DropTable(
                name: "StudentProfiles");

            migrationBuilder.DropTable(
                name: "CanvasCourses");
        }
    }
}
