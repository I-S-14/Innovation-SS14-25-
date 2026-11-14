// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using System.Linq;
using Content.Server.Database;
using Content.Shared.GameTicking;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Preferences;
using Content.Shared.Preferences.Loadouts;
using Content.Shared.Preferences.Loadouts.Effects;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Log;
using Robust.Shared.Maths;
using Robust.Shared.Network;
using Robust.UnitTesting;

namespace Content.IntegrationTests.Tests.Preferences
{
    [TestFixture]
    public sealed class ServerDbSqliteTests
    {
        [TestPrototypes]
        private const string Prototypes = @"
- type: dataset
  id: sqlite_test_names_first_male
  values:
  - Aaden

- type: dataset
  id: sqlite_test_names_first_female
  values:
  - Aaliyah

- type: dataset
  id: sqlite_test_names_last
  values:
  - Ackerley";

        private static HumanoidCharacterProfile CharlieCharlieson()
        {
            // IS14: exercise the retained appearance, TTS, traits and loadout schema.
            return new HumanoidCharacterProfile(
                "Charlie Charlieson",
                "Первый абзац.\nВторой абзац описания.",
                "Human",
                "Eugene",
                21,
                Sex.Female,
                Gender.Female,
                new(
                    "Afro",
                    Color.Aqua,
                    "Shaved",
                    Color.Aquamarine,
                    Color.Azure,
                    Color.Beige,
                    new() { new Marking("WristBraceletRight", new List<Color> { Color.Red }) }),
                SpawnPriorityPreference.Cryosleep,
                new() { ["MedicalDoctor"] = JobPriority.High, ["Passenger"] = JobPriority.Low },
                PreferenceUnavailableMode.StayInLobby,
                new() { "Traitor" },
                new() { "Blindness" },
                new()
                {
                    ["JobMedicalDoctor"] = new("JobMedicalDoctor")
                    {
                        SelectedLoadouts = new()
                        {
                            ["IS14MedicalBackpack"] = new() { new() { Prototype = "IS14MedicalDoctorSatchel" } },
                        },
                    },
                    ["JobBorg"] = new("JobBorg") { EntityName = "ИС-14 «Медик»" },
                });
        }

        private static ServerDbSqlite GetDb(RobustIntegrationTest.ServerIntegrationInstance server)
        {
            var cfg = server.ResolveDependency<IConfigurationManager>();
            var opsLog = server.ResolveDependency<ILogManager>().GetSawmill("db.ops");
            var builder = new DbContextOptionsBuilder<SqliteServerDbContext>();
            var conn = new SqliteConnection("Data Source=:memory:");
            conn.Open();
            builder.UseSqlite(conn);
            return new ServerDbSqlite(() => builder.Options, true, cfg, true, opsLog, server.ResolveDependency<Robust.Shared.Serialization.Manager.ISerializationManager>(), false);
        }

        private static void AssertProfileRoundTrip(HumanoidCharacterProfile actual, HumanoidCharacterProfile expected)
        {
            // IS14: report the damaged field instead of an opaque profile equality failure.
            using (Assert.EnterMultipleScope())
            {
                Assert.That(actual.Name, Is.EqualTo(expected.Name), "Name");
                Assert.That(actual.FlavorText, Is.EqualTo(expected.FlavorText), "FlavorText");
                Assert.That(actual.Age, Is.EqualTo(expected.Age), "Age");
                Assert.That(actual.Sex, Is.EqualTo(expected.Sex), "Sex");
                Assert.That(actual.Gender, Is.EqualTo(expected.Gender), "Gender");
                Assert.That(actual.Species, Is.EqualTo(expected.Species), "Species");
                Assert.That(actual.TTSVoice, Is.EqualTo(expected.TTSVoice), "TTSVoice");
                Assert.That(actual.SpawnPriority, Is.EqualTo(expected.SpawnPriority), "SpawnPriority");
                Assert.That(actual.PreferenceUnavailable, Is.EqualTo(expected.PreferenceUnavailable), "PreferenceUnavailable");
                Assert.That(actual.JobPriorities, Is.EquivalentTo(expected.JobPriorities), "JobPriorities");
                Assert.That(actual.AntagPreferences, Is.EquivalentTo(expected.AntagPreferences), "AntagPreferences");
                Assert.That(actual.TraitPreferences, Is.EquivalentTo(expected.TraitPreferences), "TraitPreferences");
                Assert.That(actual.Loadouts, Is.EquivalentTo(expected.Loadouts), "Loadouts");
                Assert.That(actual.Appearance.MemberwiseEquals(expected.Appearance), Is.True, "Appearance");
                Assert.That(actual.MemberwiseEquals(expected), Is.True, "Full profile comparison");
            }
        }

        [Test]
        public async Task TestUserDoesNotExist()
        {
            var pair = await PoolManager.GetServerClient();
            var db = GetDb(pair.Server);
            // Database should be empty so a new GUID should do it.
            Assert.That(await db.GetPlayerPreferencesAsync(NewUserId()), Is.Null);

            await pair.CleanReturnAsync();
        }

        [Test]
        public async Task TestInitPrefs()
        {
            var pair = await PoolManager.GetServerClient();
            var db = GetDb(pair.Server);
            var username = new NetUserId(new Guid("640bd619-fc8d-4fe2-bf3c-4a5fb17d6ddd"));
            const int slot = 0;
            var originalProfile = CharlieCharlieson();
            await db.InitPrefsAsync(username, originalProfile);
            var prefs = await db.GetPlayerPreferencesAsync(username);
            AssertProfileRoundTrip(((Content.Server.Preferences.Managers.ServerPreferencesManager)pair.Server.ResolveDependency<Content.Server.Preferences.Managers.IServerPreferencesManager>()).ConvertProfiles(prefs.Profiles.Single(p => p.Slot == slot)), originalProfile);
            await pair.CleanReturnAsync();
        }

        [Test]
        public async Task TestCharacterUpdateRetainsForkPreferences()
        {
            var pair = await PoolManager.GetServerClient();
            var server = pair.Server;
            var db = GetDb(server);
            var username = NewUserId();
            await db.InitPrefsAsync(username, new HumanoidCharacterProfile());
            var original = CharlieCharlieson();
            await db.SaveCharacterSlotAsync(username, original, 1);
            var updated = new HumanoidCharacterProfile(original)
            {
                Name = "Мария Нова",
                FlavorText = "Обновлённое описание.\nСохранено после повторной записи.",
                Appearance = new HumanoidCharacterAppearance(original.Appearance) { EyeColor = Color.Violet },
            };
            updated.Appearance.Markings[0] = new Marking("WristBraceletRight", new List<Color> { Color.Blue });
            await db.SaveCharacterSlotAsync(username, updated, 1);
            await db.SaveSelectedCharacterIndexAsync(username, 1);
            var prefs = await db.GetPlayerPreferencesAsync(username);
            var manager = (Content.Server.Preferences.Managers.ServerPreferencesManager)
                server.ResolveDependency<Content.Server.Preferences.Managers.IServerPreferencesManager>();
            var reloaded = manager.ConvertProfiles(prefs.Profiles.Single(profile => profile.Slot == 1));
            AssertProfileRoundTrip(reloaded, updated);
            await pair.CleanReturnAsync();
        }

        [Test]
        public async Task TestDeleteCharacter()
        {
            var pair = await PoolManager.GetServerClient();
            var server = pair.Server;
            var db = GetDb(server);
            var username = new NetUserId(new Guid("640bd619-fc8d-4fe2-bf3c-4a5fb17d6ddd"));
            await db.InitPrefsAsync(username, new HumanoidCharacterProfile());
            await db.SaveCharacterSlotAsync(username, CharlieCharlieson(), 1);
            await db.SaveSelectedCharacterIndexAsync(username, 1);
            await db.SaveCharacterSlotAsync(username, null, 1);
            var prefs = await db.GetPlayerPreferencesAsync(username);
            Assert.That(!prefs.Profiles.Any(p => p.Slot != 0));
            await pair.CleanReturnAsync();
        }

        [Test]
        public async Task TestNoPendingDatabaseChanges()
        {
            var pair = await PoolManager.GetServerClient();
            var server = pair.Server;
            var db = GetDb(server);
            Assert.That(async () => await db.HasPendingModelChanges(), Is.False,
                "The database has pending model changes. Add a new migration to apply them. See https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations");
            await pair.CleanReturnAsync();
        }

        private static NetUserId NewUserId()
        {
            return new(Guid.NewGuid());
        }
    }
}
