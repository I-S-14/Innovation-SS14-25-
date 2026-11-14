using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class PreserveForkUpstreamSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // IS14: tables already exist via January CustomVoteLog migrations.
            // Reconcile the snapshot lost during upstream without recreating stored votes.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Metadata reconciliation only; existing vote history must be retained.
        }
    }
}
