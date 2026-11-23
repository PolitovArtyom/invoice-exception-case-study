using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvoiceDesk.Core.Storage.Migrations
{
    /// <inheritdoc />
    public partial class StoreCorrectedFacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Corrections",
                table: "Revisions",
                newName: "Correction"
            );

            migrationBuilder.AddColumn<string>(
                name: "CorrectedFacts",
                table: "Revisions",
                type: "TEXT",
                nullable: true
            );

            migrationBuilder.AddColumn<bool>(
                name: "HasCorrectionConflict",
                table: "Revisions",
                type: "INTEGER",
                nullable: false,
                defaultValue: false
            );

            migrationBuilder.Sql(
                """
                WITH RECURSIVE
                    patches AS (
                        SELECT revision.CaseId, revision.Number, CAST(item.key AS INTEGER) AS Position,
                            json_extract(item.value, '$.field_path') AS Pointer,
                            json_extract(item.value, '$.value') AS Value,
                            json_extract(item.value, '$.missing_reason') AS MissingReason
                        FROM Revisions revision, json_each(revision.Correction, '$.items') item
                    ),
                    paths AS (
                        SELECT *,
                            CASE WHEN Pointer LIKE '/lines/%' THEN
                                '$.lines[' || substr(Pointer, 8, instr(substr(Pointer, 8), '/') - 1)
                                || '].' || substr(Pointer, 8 + instr(substr(Pointer, 8), '/'))
                            ELSE '$' || replace(Pointer, '/', '.') END AS JsonPath
                        FROM patches
                    ),
                    readings(CaseId, Number, NextPatch, Facts) AS (
                        SELECT revision.CaseId, revision.Number, 0, document.Facts
                        FROM Revisions revision
                        JOIN Documents document ON document.Id = revision.DocumentId
                        WHERE revision.Correction IS NOT NULL
                        UNION ALL
                        SELECT reading.CaseId, reading.Number, reading.NextPatch + 1,
                            json_set(reading.Facts, patch.JsonPath, patch.Value, '$.missing_fields', json((
                                SELECT json_group_array(json(entry))
                                FROM (
                                    SELECT value AS entry
                                    FROM json_each(reading.Facts, '$.missing_fields')
                                    WHERE json_extract(value, '$.field_path') <> patch.Pointer
                                    UNION ALL
                                    SELECT json_object('field_path', patch.Pointer, 'reason', patch.MissingReason)
                                    WHERE patch.MissingReason IS NOT NULL
                                )
                            )))
                        FROM readings reading
                        JOIN paths patch ON patch.CaseId = reading.CaseId
                            AND patch.Number = reading.Number AND patch.Position = reading.NextPatch
                    )
                UPDATE Revisions AS revision
                SET CorrectedFacts = (
                    SELECT reading.Facts FROM readings reading
                    WHERE reading.CaseId = revision.CaseId AND reading.Number = revision.Number
                        AND reading.NextPatch = json_array_length(revision.Correction, '$.items')
                )
                WHERE revision.Correction IS NOT NULL;

                UPDATE Revisions
                SET Correction = CASE WHEN json_extract(Action, '$.kind') = 'correct_fact'
                    THEN json_extract(Correction, '$.items[#-1]') ELSE NULL END
                WHERE Correction IS NOT NULL;
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                WITH audit AS MATERIALIZED (
                    SELECT CaseId, Number, DocumentId, Correction
                    FROM Revisions WHERE Correction IS NOT NULL
                )
                UPDATE Revisions AS revision
                SET Correction = (
                    SELECT json_object('items', json_group_array(json(entry.Correction) ORDER BY entry.Number))
                    FROM audit entry
                    WHERE entry.CaseId = revision.CaseId AND entry.DocumentId = revision.DocumentId
                        AND entry.Number <= revision.Number
                    HAVING COUNT(*) > 0
                )
                WHERE revision.CorrectedFacts IS NOT NULL;
                """
            );

            migrationBuilder.DropColumn(name: "CorrectedFacts", table: "Revisions");

            migrationBuilder.DropColumn(name: "HasCorrectionConflict", table: "Revisions");

            migrationBuilder.RenameColumn(
                name: "Correction",
                table: "Revisions",
                newName: "Corrections"
            );
        }
    }
}
