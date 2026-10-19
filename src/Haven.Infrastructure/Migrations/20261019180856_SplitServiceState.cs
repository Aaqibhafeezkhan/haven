using System;

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Haven.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SplitServiceState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_service_registry_services_service_id",
                table: "service_registry");

            migrationBuilder.DropForeignKey(
                name: "FK_service_registry_sidecars_sidecar_id",
                table: "service_registry");

            // New columns first, so the old status can be carried over before it is dropped.
            migrationBuilder.AddColumn<string>(
                name: "desired_state",
                table: "services",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Stopped");

            migrationBuilder.AddColumn<string>(
                name: "health",
                table: "service_registry",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Unknown");

            migrationBuilder.AddColumn<string>(
                name: "runtime_state",
                table: "service_registry",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Unknown");

            migrationBuilder.AddColumn<DateTime>(
                name: "state_changed_at",
                table: "service_registry",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            // Old ServiceStatus ints: 0 Running, 1 Stopped, 2 DeploymentPending, 3 Deploying, 4 Unknown.
            // Pending/Deploying were transient, so they keep the intent to run and leave the observed state Unknown
            // for the startup sync and the reconciler to correct from Docker.
            migrationBuilder.Sql(@"
                UPDATE services
                SET desired_state = CASE status WHEN 0 THEN 'Running' WHEN 2 THEN 'Running' WHEN 3 THEN 'Running' ELSE 'Stopped' END;");

            migrationBuilder.Sql(@"
                UPDATE service_registry r
                SET runtime_state = CASE s.status WHEN 0 THEN 'Running' WHEN 1 THEN 'Stopped' ELSE 'Unknown' END,
                    state_changed_at = r.updated_at
                FROM services s
                WHERE r.service_id = s.id;");

            migrationBuilder.Sql(@"
                UPDATE service_registry
                SET runtime_state = CASE status WHEN 0 THEN 'Running' WHEN 1 THEN 'Stopped' ELSE 'Unknown' END,
                    state_changed_at = updated_at
                WHERE sidecar_id IS NOT NULL;");

            // InProgress is split into Building/Starting; a stale one is a build that was interrupted.
            migrationBuilder.Sql(@"UPDATE deployments SET status = 'Building' WHERE status = 'InProgress';");

            migrationBuilder.DropColumn(
                name: "health",
                table: "sidecars");

            migrationBuilder.DropColumn(
                name: "status",
                table: "sidecars");

            migrationBuilder.DropColumn(
                name: "health",
                table: "services");

            migrationBuilder.DropColumn(
                name: "status",
                table: "services");

            migrationBuilder.DropColumn(
                name: "status",
                table: "service_registry");

            migrationBuilder.AddForeignKey(
                name: "FK_service_registry_services_service_id",
                table: "service_registry",
                column: "service_id",
                principalTable: "services",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_service_registry_sidecars_sidecar_id",
                table: "service_registry",
                column: "sidecar_id",
                principalTable: "sidecars",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_service_registry_services_service_id",
                table: "service_registry");

            migrationBuilder.DropForeignKey(
                name: "FK_service_registry_sidecars_sidecar_id",
                table: "service_registry");

            migrationBuilder.AddColumn<string>(
                name: "health",
                table: "sidecars",
                type: "text",
                nullable: false,
                defaultValue: "Unknown");

            migrationBuilder.AddColumn<int>(
                name: "status",
                table: "sidecars",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "health",
                table: "services",
                type: "text",
                nullable: false,
                defaultValue: "Unknown");

            migrationBuilder.AddColumn<int>(
                name: "status",
                table: "services",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "status",
                table: "service_registry",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            // Best effort: intent and observed state fold back into the single old status.
            migrationBuilder.Sql(@"UPDATE services SET status = CASE desired_state WHEN 'Running' THEN 0 ELSE 1 END;");
            migrationBuilder.Sql(@"UPDATE service_registry SET status = CASE runtime_state WHEN 'Running' THEN 0 WHEN 'Stopped' THEN 1 ELSE 4 END;");
            migrationBuilder.Sql(@"UPDATE sidecars SET status = CASE WHEN enabled THEN 0 ELSE 1 END;");
            migrationBuilder.Sql(@"UPDATE deployments SET status = 'InProgress' WHERE status IN ('Queued', 'Building', 'Starting');");

            migrationBuilder.DropColumn(
                name: "desired_state",
                table: "services");

            migrationBuilder.DropColumn(
                name: "health",
                table: "service_registry");

            migrationBuilder.DropColumn(
                name: "runtime_state",
                table: "service_registry");

            migrationBuilder.DropColumn(
                name: "state_changed_at",
                table: "service_registry");

            migrationBuilder.AddForeignKey(
                name: "FK_service_registry_services_service_id",
                table: "service_registry",
                column: "service_id",
                principalTable: "services",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_service_registry_sidecars_sidecar_id",
                table: "service_registry",
                column: "sidecar_id",
                principalTable: "sidecars",
                principalColumn: "id");
        }
    }
}