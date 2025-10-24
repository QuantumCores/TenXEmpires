using TenXEmpires.Server.Domain.Services;

namespace TenXEmpires.Server.Infrastructure.Services;

/// <summary>
/// Generates AI opponent names using historical and fantasy leaders.
/// </summary>
public class AiNameGenerator : IAiNameGenerator
{
    private static readonly string[] AiNames = new[]
    {
        // Ancient Leaders
        "Alexander the Great",
        "Julius Caesar",
        "Cleopatra VII",
        "Hannibal Barca",
        "Genghis Khan",
        "Cyrus the Great",
        "Ramesses II",
        "Qin Shi Huang",
        "Leonidas I",
        "Attila the Hun",
        
        // Medieval & Renaissance
        "Charlemagne",
        "William the Conqueror",
        "Saladin",
        "Richard the Lionheart",
        "Joan of Arc",
        "Suleiman the Magnificent",
        "Isabella I of Castile",
        "Mehmed II",
        "Timur",
        "Frederick Barbarossa",
        
        // Fantasy Leaders (The Witcher)
        "Emhyr var Emreis",
        "Foltest of Temeria",
        "Radovid V",
        "Demavend III",
        "Henselt of Kaedwen",
        
        // Fantasy Leaders (Game of Thrones inspired)
        "Aegon the Conqueror",
        "Daenerys Stormborn",
        "Robert Baratheon",
        "Tywin Lannister",
        "Robb Stark",
        
        // Fantasy Leaders (LOTR inspired)
        "Aragorn Elessar",
        "Elendil the Tall",
        "Thranduil",
        "Denethor II",
        "Théoden King",
        
        // Strategy Game Classics
        "Gilgamesh",
        "Gandhi",
        "Montezuma",
        "Peter the Great",
        "Catherine the Great",
        "Napoleon Bonaparte",
        "Wu Zetian",
        "Tokugawa Ieyasu",
        "Sejong the Great",
        "Harun al-Rashid"
    };

    public string GenerateName(long? seed = null)
    {
        Random random;
        
        if (seed.HasValue)
        {
            // Use seed for deterministic generation
            random = new Random((int)(seed.Value % int.MaxValue));
        }
        else
        {
            // Use thread-safe random for non-deterministic generation
            random = Random.Shared;
        }

        var index = random.Next(0, AiNames.Length);
        return AiNames[index];
    }
}

