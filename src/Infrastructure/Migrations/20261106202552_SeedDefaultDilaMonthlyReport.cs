using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManagementApi.Infrastructure.Migrations
{
    /// <summary>
    /// Seeds the organisation's Dila "Monthly Activities Report" (report type GMR, monthly), the same
    /// 13 Qiadat sections as the HRS default template. Ordinary data: the Secretariat can deactivate it
    /// or publish a new version like any other template.
    ///
    /// HRS field types map to this app's question types: currency → Number (help text "Amount in ₦"),
    /// repeatable tables → a text area listing the columns, "show if" → a help-text note.
    /// Ids are derived from stable keys so the seed is the same in every environment.
    /// </summary>
    public partial class SeedDefaultDilaMonthlyReport : Migration
    {
        private const int Dila = 2;          // OrganizationLevel.Dila
        private const int Monthly = 1;       // ReportFrequency.Monthly
        private const int Text = 1, TextArea = 2, Number = 3, YesNo = 8; // QuestionType

        private static readonly DateTime SeedTime = new(2026, 11, 6, 0, 0, 0, DateTimeKind.Utc);
        private static readonly Guid ReportTypeId = Id("report-type:dila-monthly");
        private static readonly Guid TemplateId = Id("template:dila-monthly:v1");

        private record Q(string Key, int Type, string Text, string Help = null);

        private record S(string Key, string Title, Q[] Questions);

        private static Q Num(string key, string text, string help = null) => new(key, Number, text, help);

        private static Q Money(string key, string text) => new(key, Number, text, "Amount in ₦");

        private static Q Short(string key, string text) => new(key, Text, text);

        private static Q Long(string key, string text) => new(key, TextArea, text);

        private static Q Bool(string key, string text) => new(key, YesNo, text);

        private static Q Table(string key, string text, params string[] columns) =>
            new(key, TextArea, text, $"One line per entry: {string.Join(", ", columns)}");

        private static string OnlyIf(string question) => $"Answer only if \"{question}\" is Yes.";

        private static readonly S[] Sections =
        {
            new("umumi", "Qiadat Umumi — Administration", new[]
            {
                Bool("executive_meeting_held", "Executive meeting held?"),
                Num("executive_meeting_attendance", "Executive meeting attendance", OnlyIf("Executive meeting held?")),
                Bool("general_meeting_held", "General meeting held?"),
                Num("general_meeting_attendance", "General meeting attendance", OnlyIf("General meeting held?")),
                Short("dila_email", "Dil'a email address"),
                Short("nazim_aala_visits", "Visits by Nazim A'ala to Muqam"),
            }),
            new("tajnid", "Qiadat Tajnid — Census", new[]
            {
                Num("number_of_muqam", "Number of Muqam"),
                Num("number_of_halqa", "Number of Halqa"),
                Num("number_of_saf_awwal", "Number of Saf Awwal"),
                Num("number_of_saf_dom", "Number of Saf Dom"),
                Num("total_membership", "Total membership"),
            }),
            new("talim_quran", "Qiadat Ta'limul-Qur'an — Knowledge of the Holy Qur'an", new[]
            {
                Num("talim_majalis_organizing_saf_dom", "Majalis organizing Ta'leemul Qur'an — Saf Dom"),
                Num("talim_majalis_organizing_saf_awwal", "Majalis organizing Ta'leemul Qur'an — Saf Awwal"),
                Num("talim_participated_saf_dom", "Participated in Ta'leemul Qur'an — Saf Dom"),
                Num("talim_participated_saf_awwal", "Participated in Ta'leemul Qur'an — Saf Awwal"),
                Num("talim_yassarnal_level_saf_dom", "On Yassarnal Qur'an learning level — Saf Dom"),
                Num("talim_yassarnal_level_saf_awwal", "On Yassarnal Qur'an learning level — Saf Awwal"),
                Num("talim_recitation_level_saf_dom", "On Recitation learning level — Saf Dom"),
                Num("talim_recitation_level_saf_awwal", "On Recitation learning level — Saf Awwal"),
                Num("talim_recite_fluently_saf_dom", "Can recite fluently and correctly — Saf Dom"),
                Num("talim_recite_fluently_saf_awwal", "Can recite fluently and correctly — Saf Awwal"),
                Num("talim_completed_yassarnal_saf_dom", "Completed Yassarnal Qur'an this month — Saf Dom"),
                Num("talim_completed_yassarnal_saf_awwal", "Completed Yassarnal Qur'an this month — Saf Awwal"),
                Num("talim_completed_recitation_saf_dom", "Completed simple recitation this month — Saf Dom"),
                Num("talim_completed_recitation_saf_awwal", "Completed simple recitation this month — Saf Awwal"),
                Num("talim_learning_translation_saf_dom", "Learning translation (split meaning) — Saf Dom"),
                Num("talim_learning_translation_saf_awwal", "Learning translation (split meaning) — Saf Awwal"),
                Num("talim_studied_commentary_saf_dom", "Studied commentary this month — Saf Dom"),
                Num("talim_studied_commentary_saf_awwal", "Studied commentary this month — Saf Awwal"),
                Num("talim_memorised_baqarah_saf_dom", "Memorised Surah Baqarah (v. 1–17) — Saf Dom"),
                Num("talim_memorised_baqarah_saf_awwal", "Memorised Surah Baqarah (v. 1–17) — Saf Awwal"),
            }),
            new("tarbiyyat", "Qiadat Tarbiyyat — Moral Training", new[]
            {
                Num("dila_executive_members", "Dil'a executive members"),
                Num("observed_jumaat", "Ansarullah who observed Juma'at Prayer"),
                Num("observed_nafil_fast", "Ansar who observed Nafil Fast"),
                Num("salat_in_congregation", "Offered Salat in congregation"),
                Num("offered_five_daily_prayers", "Offered 5 daily prayers"),
                Num("recited_quran_daily", "Recited Qur'an daily"),
                Num("listened_huzur_sermon", "Listened to Huzur sermon"),
                Num("watch_mta", "Watch MTA"),
                Num("salat_awareness_days", "Salat awareness days"),
                Num("total_nau_mubaeen", "Total Nau Mubaeen (new Ahmadis)"),
                Num("active_nau_mubaeen", "Active Nau Mubaeen"),
                Num("nau_mubaeen_programmes", "Programmes organised for Nau Mubaeen"),
                Num("new_nau_mubaeen_this_month", "New Nau Mubaeen this month"),
            }),
            new("tabligh", "Qiadat Tabligh — Preaching", new[]
            {
                Num("number_of_dai_illah", "Number of Dai-illah"),
                Num("one_to_one_preaching", "One-to-one preaching"),
                Num("tabligh_exhibitions", "Tabligh exhibitions"),
                Num("literatures_distributed", "Literatures distributed"),
                Num("tabligh_stalls", "Tabligh stalls"),
                Num("baiat_up_to_now", "Baiat up to now"),
                Num("new_converts", "New converts"),
                Num("baiat_this_month", "Baiat this month"),
                Num("persons_preached_to", "Persons preached to"),
                Num("participants_in_tabligh", "Participants in tabligh"),
                Num("house_to_house_preaching", "House-to-house preaching"),
                Num("public_lectures_held", "Public lectures held"),
                Long("tabligh_comments", "Comments / Observations"),
            }),
            new("ithar", "Qiadat Ithar — Welfare", new[]
            {
                Bool("welfare_committee_in_place", "Welfare committee in place?"),
                Num("welfare_matters_treated", "Welfare matters treated between members"),
                Num("members_paid_welfare_fund", "Members who paid welfare fund"),
                Num("members_paid_special_welfare", "Members who paid special welfare"),
                Num("visits_to_welfare_houses", "Visits to welfare houses"),
                Num("people_in_welfare_visits", "People involved in welfare visits"),
                Num("single_members", "Single members"),
                Num("widowed_members", "Widowed members"),
                Num("separated_members", "Separated members"),
                Num("unemployed_members", "Unemployed members"),
                Num("sick_members_visited", "Sick members visited"),
                Num("elderly_members_visited", "Elderly members visited"),
                Money("amount_spent_on_welfare", "Amount spent on welfare matters"),
                Long("efforts_engage_inactive", "Efforts to engage inactive members"),
            }),
            new("ishaat", "Qiadat Isha'at — Publication", new[]
            {
                Table("publication_table", "Publications", "Publication", "Copies collected", "Amount paid (₦)", "Amount remaining (₦)"),
                Short("procurement_effort", "Effort to ensure procurement"),
            }),
            new("tahrik_i_jadid", "Qiadat Tahrik-i-Jadid", new[]
            {
                Num("tj_promisors", "Number of promisors"),
                Num("tj_payers", "Number of payers for the month"),
                Money("tj_amount_paid", "Amount paid"),
                Money("tj_amount_remaining", "Amount remaining"),
            }),
            new("waqf_e_jadid", "Qiadat Waqf-e-Jadid", new[]
            {
                Num("wj_promisors", "Number of promisors"),
                Num("wj_payers", "Number of payers for the month"),
                Money("wj_amount_paid", "Amount paid"),
                Money("wj_amount_remaining", "Amount remaining"),
                Short("wj_encourage_participation", "Efforts to encourage participation"),
            }),
            new("mal", "Mal — Finance", new[]
            {
                Table("chanda_breakdown", "Chanda breakdown", "Chanda category", "Amount (₦)"),
                Money("chanda_grand_total", "Grand total"),
                Table("contributions_by_muqam", "Contributions by Muqam", "Muqam", "No. of payers", "Amount paid (₦)"),
            }),
            new("health", "Zehanat & Sehat-e-Jismani — Health / Physical Training", new[]
            {
                Bool("health_programme_held", "Health programme held?"),
                Num("ansar_in_regular_exercise", "Ansar in regular exercise"),
                Num("ansarullah_with_bicycles", "Ansarullah with bicycles"),
            }),
            new("talim", "Qiadat Ta'lim — Education", new[]
            {
                Num("promised_messiah_book_read", "Promised Messiah (AS) book read this month"),
                Num("ansar_reading_book", "Ansar reading the book"),
                Num("ansar_participated_assessment", "Ansar who participated in assessment"),
                Num("literate_saf_dom", "Number of Literate — Saf Dom"),
                Num("literate_saf_awwal", "Number of Literate — Saf Awwal"),
                Num("msc_ma_med_holders", "MSc / MA / M.Ed holders"),
                Num("bsc_ba_bed_holders", "BSc / BA / B.Ed holders"),
            }),
            new("correspondences", "Correspondences & Other Information", new[]
            {
                Num("letters_from_mulk", "Letters / Circulars received from Mulk"),
                Num("letters_from_jamaat", "Letters / Circulars received from Jama'at"),
                Num("letters_replied", "Letters / Circulars replied to"),
                Num("dila_report_presented", "Dil'a report presented"),
                Long("other_information", "Any other information"),
                Short("coordinator_name", "Name of Coordinator / Nazim A'ala"),
                Short("coordinator_phone", "Coordinator phone"),
            }),
        };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "ReportTypes",
                columns: new[] { "Id", "Name", "Code", "Description", "Frequency", "IsActive", "CreatedOn" },
                values: new object[]
                {
                    ReportTypeId,
                    "Dila Monthly Activities Report",
                    "GMR",
                    "Standard Dila-level monthly report covering administration, census, education, moral training, preaching, welfare, publications, finance and correspondence.",
                    Monthly,
                    true,
                    SeedTime,
                });

            migrationBuilder.InsertData(
                table: "ReportTemplates",
                columns: new[] { "Id", "Name", "Description", "ReportType", "ReportTypeId", "OrganizationLevel", "IsForAllMembers", "IsActive", "DisplayOrder", "Version", "RootTemplateId", "CreatedOn" },
                values: new object[]
                {
                    TemplateId,
                    "Dila Monthly Activities Report",
                    "Monthly report from each Dila to the National Secretariat.",
                    "Dila Monthly Activities Report",
                    ReportTypeId,
                    Dila,
                    false,
                    true,
                    0,
                    1,
                    null,
                    SeedTime,
                });

            for (var s = 0; s < Sections.Length; s++)
            {
                var section = Sections[s];
                var sectionId = Id($"template:dila-monthly:v1:section:{section.Key}");

                migrationBuilder.InsertData(
                    table: "ReportSections",
                    columns: new[] { "Id", "ReportTemplateId", "Title", "Description", "DisplayOrder", "IsRequired", "CreatedOn" },
                    values: new object[] { sectionId, TemplateId, section.Title, null, s + 1, false, SeedTime });

                for (var q = 0; q < section.Questions.Length; q++)
                {
                    var question = section.Questions[q];
                    migrationBuilder.InsertData(
                        table: "ReportQuestions",
                        columns: new[] { "Id", "ReportSectionId", "QuestionText", "HelpText", "QuestionType", "Options", "IsRequired", "DisplayOrder", "ValidationRules", "CreatedOn" },
                        values: new object[]
                        {
                            Id($"template:dila-monthly:v1:question:{question.Key}"),
                            sectionId,
                            question.Text,
                            question.Help,
                            question.Type,
                            null,
                            false,
                            q + 1,
                            null,
                            SeedTime,
                        });
                }
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Questions and sections cascade from the template.
            migrationBuilder.DeleteData(table: "ReportTemplates", keyColumn: "Id", keyValue: TemplateId);
            migrationBuilder.DeleteData(table: "ReportTypes", keyColumn: "Id", keyValue: ReportTypeId);
        }

        private static Guid Id(string key) => new(MD5.HashData(Encoding.UTF8.GetBytes("maan-seed:" + key)));
    }
}
