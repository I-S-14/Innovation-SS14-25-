using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class VoiceSelector : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            //IS14-change: TTSVoice (2022) already owns this column. SplitTTSVoice
            // (2026-07-11) preserves it as ttsvoice and creates the new voice column.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            //IS14-change: no schema operation was performed by Up.
        }
    }
}
