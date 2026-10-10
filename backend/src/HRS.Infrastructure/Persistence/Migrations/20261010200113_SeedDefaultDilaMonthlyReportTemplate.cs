using System.Text.Json;
using HRS.Application.Features.Templates.TemplateVersions;
using HRS.Domain.Organization;
using HRS.Domain.Templates;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRS.Infrastructure.Persistence.Migrations;

/// <summary>
/// Seeds the organization's real Dila-level "Monthly Activities Report" — the Qiadat sections
/// (Umumi, Tajnid, Ta'limul-Qur'an, Tarbiyyat, Tabligh, Ithar, Isha'at, Tahrik-i-Jadid,
/// Waqf-e-Jadid, Mal, Zehanat &amp; Sehat-e-Jismani, Ta'lim, Correspondences) from the
/// organization's existing paper/PDF report. v1 is single-tenant (hrs-domain skill: "Out of
/// scope in v1 ... multi-tenancy"), so — unlike UnitLevels/the National unit, which are
/// structural data — this is ordinary, editable ReportType/TemplateVersion data seeded once so
/// every environment starts with the organization's actual report ready to use, the same way a
/// Secretariat user would build it through the Template Builder. A plain data migration
/// (InsertData, not AppDbContext HasData) on purpose: HasData would make this part of the EF
/// model forever; this is meant to behave exactly like any other Secretariat-created report
/// type from the moment it exists — editable, archivable, replaceable.
/// </summary>
public partial class SeedDefaultDilaMonthlyReportTemplate : Migration
{
    private static readonly Guid ReportTypeId = new("00000000-0000-0000-0000-0000000000a1");
    private static readonly Guid TemplateVersionId = new("00000000-0000-0000-0000-0000000000a2");
    private static readonly DateTimeOffset SeedTimestamp = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.InsertData(
            table: "report_types",
            columns: ["id", "name", "description", "category", "frequency", "applicable_levels", "recipient_level", "observer_depth", "allow_attachments", "allow_nil_report", "created_at", "archived_at"],
            values: new object[]
            {
                ReportTypeId,
                "Dila Monthly Activities Report",
                "Standard Dila-level monthly report covering administration, census, education, moral training, preaching, welfare, publications, finance and correspondence.",
                (int)ReportTypeCategory.GeneralMonthly,
                (int)ReportTypeFrequency.Monthly,
                new[] { (int)UnitLevelCode.Dila },
                (int)UnitLevelCode.National,
                1,
                true,
                true,
                SeedTimestamp,
                null,
            });

        migrationBuilder.InsertData(
            table: "template_versions",
            columns: ["id", "report_type_id", "version_number", "status", "schema_json", "created_at", "published_at", "retired_at"],
            values: new object[]
            {
                TemplateVersionId,
                ReportTypeId,
                1,
                (int)TemplateVersionStatus.Published,
                JsonSerializer.Serialize(BuildSchema(), JsonSerializerOptions.Web),
                SeedTimestamp,
                SeedTimestamp,
                null,
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DeleteData(table: "template_versions", keyColumn: "id", keyValue: TemplateVersionId);
        migrationBuilder.DeleteData(table: "report_types", keyColumn: "id", keyValue: ReportTypeId);
    }

    private static TemplateField Field(
        string key, TemplateFieldType type, string label, AggregationRule aggregation = AggregationRule.None,
        TemplateShowIf showIf = null, string unitLabel = null, IReadOnlyList<TemplateField> columns = null) =>
        new(key, type, label, null, false, null, null, null, null, unitLabel, false, null, null, showIf, aggregation, null, null, columns);

    private static TemplateField Number(string key, string label, TemplateShowIf showIf = null) =>
        Field(key, TemplateFieldType.Number, label, showIf: showIf);

    private static TemplateField Currency(string key, string label) =>
        Field(key, TemplateFieldType.Currency, label, unitLabel: "₦");

    private static TemplateField ShortText(string key, string label) =>
        Field(key, TemplateFieldType.ShortText, label);

    private static TemplateField LongText(string key, string label) =>
        Field(key, TemplateFieldType.LongText, label);

    private static TemplateField YesNo(string key, string label) =>
        Field(key, TemplateFieldType.YesNo, label);

    private static TemplateField RepeatableTable(string key, string label, params TemplateField[] columns) =>
        Field(key, TemplateFieldType.RepeatableTable, label, columns: columns);

    private static TemplateShowIf WhenYes(string fieldKey) => new(fieldKey, "true");

    private static TemplateSchema BuildSchema() => new([
        new TemplateSection("umumi", "Qiadat Umumi — Administration", [
            YesNo("executive_meeting_held", "Executive meeting held?"),
            Number("executive_meeting_attendance", "Executive meeting attendance", WhenYes("executive_meeting_held")),
            YesNo("general_meeting_held", "General meeting held?"),
            Number("general_meeting_attendance", "General meeting attendance", WhenYes("general_meeting_held")),
            ShortText("dila_email", "Dil'a email address"),
            ShortText("nazim_aala_visits", "Visits by Nazim A'ala to Muqam"),
        ]),
        new TemplateSection("tajnid", "Qiadat Tajnid — Census", [
            Number("number_of_muqam", "Number of Muqam"),
            Number("number_of_halqa", "Number of Halqa"),
            Number("number_of_saf_awwal", "Number of Saf Awwal"),
            Number("number_of_saf_dom", "Number of Saf Dom"),
            Number("total_membership", "Total membership"),
        ]),
        new TemplateSection("talim_quran", "Qiadat Ta'limul-Qur'an — Knowledge of the Holy Qur'an", [
            Number("talim_majalis_organizing_saf_dom", "Majalis organizing Ta'leemul Qur'an — Saf Dom"),
            Number("talim_majalis_organizing_saf_awwal", "Majalis organizing Ta'leemul Qur'an — Saf Awwal"),
            Number("talim_participated_saf_dom", "Participated in Ta'leemul Qur'an — Saf Dom"),
            Number("talim_participated_saf_awwal", "Participated in Ta'leemul Qur'an — Saf Awwal"),
            Number("talim_yassarnal_level_saf_dom", "On Yassarnal Qur'an learning level — Saf Dom"),
            Number("talim_yassarnal_level_saf_awwal", "On Yassarnal Qur'an learning level — Saf Awwal"),
            Number("talim_recitation_level_saf_dom", "On Recitation learning level — Saf Dom"),
            Number("talim_recitation_level_saf_awwal", "On Recitation learning level — Saf Awwal"),
            Number("talim_recite_fluently_saf_dom", "Can recite fluently and correctly — Saf Dom"),
            Number("talim_recite_fluently_saf_awwal", "Can recite fluently and correctly — Saf Awwal"),
            Number("talim_completed_yassarnal_saf_dom", "Completed Yassarnal Qur'an this month — Saf Dom"),
            Number("talim_completed_yassarnal_saf_awwal", "Completed Yassarnal Qur'an this month — Saf Awwal"),
            Number("talim_completed_recitation_saf_dom", "Completed simple recitation this month — Saf Dom"),
            Number("talim_completed_recitation_saf_awwal", "Completed simple recitation this month — Saf Awwal"),
            Number("talim_learning_translation_saf_dom", "Learning translation (split meaning) — Saf Dom"),
            Number("talim_learning_translation_saf_awwal", "Learning translation (split meaning) — Saf Awwal"),
            Number("talim_studied_commentary_saf_dom", "Studied commentary this month — Saf Dom"),
            Number("talim_studied_commentary_saf_awwal", "Studied commentary this month — Saf Awwal"),
            Number("talim_memorised_baqarah_saf_dom", "Memorised Surah Baqarah (v. 1–17) — Saf Dom"),
            Number("talim_memorised_baqarah_saf_awwal", "Memorised Surah Baqarah (v. 1–17) — Saf Awwal"),
        ]),
        new TemplateSection("tarbiyyat", "Qiadat Tarbiyyat — Moral Training", [
            Number("dila_executive_members", "Dil'a executive members"),
            Number("observed_jumaat", "Ansarullah who observed Juma'at Prayer"),
            Number("observed_nafil_fast", "Ansar who observed Nafil Fast"),
            Number("salat_in_congregation", "Offered Salat in congregation"),
            Number("offered_five_daily_prayers", "Offered 5 daily prayers"),
            Number("recited_quran_daily", "Recited Qur'an daily"),
            Number("listened_huzur_sermon", "Listened to Huzur sermon"),
            Number("watch_mta", "Watch MTA"),
            Number("salat_awareness_days", "Salat awareness days"),
            Number("total_nau_mubaeen", "Total Nau Mubaeen (new Ahmadis)"),
            Number("active_nau_mubaeen", "Active Nau Mubaeen"),
            Number("nau_mubaeen_programmes", "Programmes organised for Nau Mubaeen"),
            Number("new_nau_mubaeen_this_month", "New Nau Mubaeen this month"),
        ]),
        new TemplateSection("tabligh", "Qiadat Tabligh — Preaching", [
            Number("number_of_dai_illah", "Number of Dai-illah"),
            Number("one_to_one_preaching", "One-to-one preaching"),
            Number("tabligh_exhibitions", "Tabligh exhibitions"),
            Number("literatures_distributed", "Literatures distributed"),
            Number("tabligh_stalls", "Tabligh stalls"),
            Number("baiat_up_to_now", "Baiat up to now"),
            Number("new_converts", "New converts"),
            Number("baiat_this_month", "Baiat this month"),
            Number("persons_preached_to", "Persons preached to"),
            Number("participants_in_tabligh", "Participants in tabligh"),
            Number("house_to_house_preaching", "House-to-house preaching"),
            Number("public_lectures_held", "Public lectures held"),
            LongText("tabligh_comments", "Comments / Observations"),
        ]),
        new TemplateSection("ithar", "Qiadat Ithar — Welfare", [
            YesNo("welfare_committee_in_place", "Welfare committee in place?"),
            Number("welfare_matters_treated", "Welfare matters treated between members"),
            Number("members_paid_welfare_fund", "Members who paid welfare fund"),
            Number("members_paid_special_welfare", "Members who paid special welfare"),
            Number("visits_to_welfare_houses", "Visits to welfare houses"),
            Number("people_in_welfare_visits", "People involved in welfare visits"),
            Number("single_members", "Single members"),
            Number("widowed_members", "Widowed members"),
            Number("separated_members", "Separated members"),
            Number("unemployed_members", "Unemployed members"),
            Number("sick_members_visited", "Sick members visited"),
            Number("elderly_members_visited", "Elderly members visited"),
            Currency("amount_spent_on_welfare", "Amount spent on welfare matters"),
            LongText("efforts_engage_inactive", "Efforts to engage inactive members"),
        ]),
        new TemplateSection("ishaat", "Qiadat Isha'at — Publication", [
            RepeatableTable("publication_table", "Publications",
                ShortText("publication_name", "Publication"),
                Number("copies_collected", "Copies collected"),
                Currency("amount_paid", "Amount paid"),
                Currency("amount_remaining", "Amount remaining")),
            ShortText("procurement_effort", "Effort to ensure procurement"),
        ]),
        new TemplateSection("tahrik_i_jadid", "Qiadat Tahrik-i-Jadid", [
            Number("tj_promisors", "Number of promisors"),
            Number("tj_payers", "Number of payers for the month"),
            Currency("tj_amount_paid", "Amount paid"),
            Currency("tj_amount_remaining", "Amount remaining"),
        ]),
        new TemplateSection("waqf_e_jadid", "Qiadat Waqf-e-Jadid", [
            Number("wj_promisors", "Number of promisors"),
            Number("wj_payers", "Number of payers for the month"),
            Currency("wj_amount_paid", "Amount paid"),
            Currency("wj_amount_remaining", "Amount remaining"),
            ShortText("wj_encourage_participation", "Efforts to encourage participation"),
        ]),
        new TemplateSection("mal", "Mal — Finance", [
            RepeatableTable("chanda_breakdown", "Chanda breakdown",
                ShortText("chanda_category", "Chanda category"),
                Currency("chanda_amount", "Amount")),
            Currency("chanda_grand_total", "Grand total"),
            RepeatableTable("contributions_by_muqam", "Contributions by Muqam",
                ShortText("contribution_muqam_name", "Muqam"),
                Number("contribution_payers", "No. of payers"),
                Currency("contribution_amount", "Amount paid")),
        ]),
        new TemplateSection("health", "Zehanat & Sehat-e-Jismani — Health / Physical Training", [
            YesNo("health_programme_held", "Health programme held?"),
            Number("ansar_in_regular_exercise", "Ansar in regular exercise"),
            Number("ansarullah_with_bicycles", "Ansarullah with bicycles"),
        ]),
        new TemplateSection("talim", "Qiadat Ta'lim — Education", [
            Number("promised_messiah_book_read", "Promised Messiah (AS) book read this month"),
            Number("ansar_reading_book", "Ansar reading the book"),
            Number("ansar_participated_assessment", "Ansar who participated in assessment"),
            Number("literate_saf_dom", "Number of Literate — Saf Dom"),
            Number("literate_saf_awwal", "Number of Literate — Saf Awwal"),
            Number("msc_ma_med_holders", "MSc / MA / M.Ed holders"),
            Number("bsc_ba_bed_holders", "BSc / BA / B.Ed holders"),
        ]),
        new TemplateSection("correspondences", "Correspondences & Other Information", [
            Number("letters_from_mulk", "Letters / Circulars received from Mulk"),
            Number("letters_from_jamaat", "Letters / Circulars received from Jama'at"),
            Number("letters_replied", "Letters / Circulars replied to"),
            Number("dila_report_presented", "Dil'a report presented"),
            LongText("other_information", "Any other information"),
            ShortText("coordinator_name", "Name of Coordinator / Nazim A'ala"),
            ShortText("coordinator_phone", "Coordinator phone"),
        ]),
    ]);
}
