using System.Linq;
using Content.Shared.CartridgeLoader;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class LegacyPdaProgramsTest
{
    [TestCase("PassengerPDA", "CrewManifestCartridge,NotekeeperCartridge,NanoTaskCartridge,NewsReaderCartridge,NanoChatCartridge")]
    [TestCase("SecurityCadetPDA", "CrewManifestCartridge,NotekeeperCartridge,NanoTaskCartridge,NewsReaderCartridge,WantedListCartridge,NanoChatCartridge,IS14FineCartridge")]
    [TestCase("MedicalInternPDA", "CrewManifestCartridge,NotekeeperCartridge,NanoTaskCartridge,NewsReaderCartridge,MedTekCartridge,NanoChatCartridge")]
    [TestCase("QuartermasterPDA", "CrewManifestCartridge,NotekeeperCartridge,NanoTaskCartridge,NewsReaderCartridge,AstroNavCartridge,NanoChatCartridge")]
    [TestCase("SalvagePDA", "CrewManifestCartridge,NotekeeperCartridge,NanoTaskCartridge,NewsReaderCartridge,AstroNavCartridge,NanoChatCartridge")]
    [TestCase("HoSPDA", "CrewManifestCartridge,NotekeeperCartridge,NanoTaskCartridge,NewsReaderCartridge,WantedListCartridge,LogProbeCartridge,NanoChatCartridge")]
    [TestCase("CentcomPDA", "CrewManifestCartridge,NotekeeperCartridge,NanoTaskCartridge,NewsReaderCartridge,MedTekCartridge,WantedListCartridge,LogProbeCartridge,AstroNavCartridge,NanoChatCartridge")]
    [TestCase("AdminPDA", "CrewManifestCartridge,NotekeeperCartridge,NanoTaskCartridge,NewsReaderCartridge,LogProbeCartridge,WantedListCartridge,MedTekCartridge,AstroNavCartridge,NanoChatCartridge")]
    [TestCase("SyndiPDA", "NotekeeperCartridge,NanoTaskCartridge,NanoChatCartridge")]
    [TestCase("ERTLeaderPDA", "CrewManifestCartridge,NotekeeperCartridge,NanoTaskCartridge,NewsReaderCartridge,MedTekCartridge,WantedListCartridge,LogProbeCartridge,AstroNavCartridge,NanoChatCartridge")]
    [TestCase("CBURNPDA", "CrewManifestCartridge,NotekeeperCartridge,NanoTaskCartridge,NewsReaderCartridge,MedTekCartridge,WantedListCartridge,LogProbeCartridge,AstroNavCartridge,NanoChatCartridge")]
    [TestCase("DetectivePDA", "CrewManifestCartridge,NotekeeperCartridge,NanoTaskCartridge,NewsReaderCartridge,WantedListCartridge,LogProbeCartridge,NanoChatCartridge")]
    [TestCase("BrigmedicPDA", "CrewManifestCartridge,NotekeeperCartridge,NanoTaskCartridge,NewsReaderCartridge,WantedListCartridge,MedTekCartridge,NanoChatCartridge")]
    [TestCase("SyndiAgentPDA", "NotekeeperCartridge,NanoTaskCartridge,MedTekCartridge,NanoChatCartridge")]
    [TestCase("NavyCaptainPDA", "CrewManifestCartridge,NotekeeperCartridge,NewsReaderCartridge,LogProbeCartridge,WantedListCartridge,MedTekCartridge,AstroNavCartridge,NanoChatCartridge")]
    [TestCase("HighCommandPDA", "NotekeeperCartridge,NanoTaskCartridge,NanoChatCartridge")]
    [TestCase("StationAiPDA", "CrewManifestCartridge,NotekeeperCartridge,NewsReaderCartridge,NanoChatCartridge,WantedListCartridge")]
    [TestCase("MinerPDA", "CrewManifestCartridge,NotekeeperCartridge,NanoTaskCartridge,NewsReaderCartridge,AstroNavCartridge,NanoChatCartridge")]

    public async Task SpawnInstallsOriginalProgramsAsReadonly(string prototype, string expectedPrograms)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var entity = server.EntMan.SpawnEntity(prototype, map.GridCoords);
            var programs = server.System<CartridgeLoaderSystem>().GetPreinstalledPrograms(entity);
            var actual = programs.Select(uid => server.EntMan.GetComponent<MetaDataComponent>(uid).EntityPrototype!.ID).ToArray();
            Assert.That(actual, Is.EquivalentTo(expectedPrograms.Split(',')));
            foreach (var program in programs)
            {
                var cartridge = server.EntMan.GetComponent<CartridgeComponent>(program);
                Assert.That(cartridge.LoaderUid, Is.EqualTo(entity));
                Assert.That(cartridge.InstallationStatus, Is.EqualTo(InstallationStatus.Readonly));
            }
            server.EntMan.DeleteEntity(entity);
        });
        await pair.CleanReturnAsync();
    }
}
