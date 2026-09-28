namespace AquaGames.League
{
    // Display names for simulated players: mix of first names, handles and short gamer tags,
    // international like a real global leaderboard. Replace or extend freely.
    public static class NamePool
    {
        public static string[] Names =
        {
            "Emma", "Liam", "Olivia", "Noah", "Ava", "Mateo", "Sofia", "Lucas", "Mia", "Arjun", "Aisha", "Yuki", "Hana", "Omar",
            "Zara", "Leo", "Nina", "Ivan", "Chloe", "Diego", "Priya", "Kenji", "Fatima", "Jonas", "Elif", "Mehmet", "Sara", "Ali",
            "Lena", "Hugo", "Maya", "Rafael", "Ines", "Tomás", "Chen", "Wei", "Min-jun", "Ji-woo", "Amir", "Layla", "Sven", "Freya",
            "Kofi", "Amara", "Tariq", "Noor", "Pablo", "Lucia", "Anya", "Dmitri", "Ella", "Oscar", "Ruby", "Finn", "Isla", "Jack",
            "Grace", "Kai", "Luna", "Ezra", "Aria", "Milo", "Nora", "Theo", "Ivy", "Felix", "Ada", "Hassan", "Selin", "Bruno",
            "Camila", "Andrés", "Valentina", "Mohammed", "Yusuf", "Zeynep", "Kaito", "Sakura", "Riya", "Vikram", "Ananya", "Joao",
            "Beatriz", "Mariam", "Karim", "Elena", "Marco", "Giulia", "Pierre", "Camille", "Anna", "Jakub", "Zofia", "Lars", "Ingrid",
        };

        public static string[] Handles =
        {
            "pixelpro", "sawqueen", "blade_runner", "cubecrusher", "nightowl", "lazycat", "speedy_g", "mr_blue", "sunny", "the_real_max",
            "gamerella", "ninja42", "coffeeaddict", "puzzlehead", "starfox", "moonwalker", "turbo", "kiwi", "bubbles", "captainK",
            "zen_master", "lucky7", "happyhippo", "rocketman", "fluffy", "shadow", "breezy", "maplefox", "orbit", "sparky",
        };

        public static string Pick(System.Random rng)
        {
            double r = rng.NextDouble();
            if (r < 0.55) return Names[rng.Next(Names.Length)];
            if (r < 0.80) return Names[rng.Next(Names.Length)] + " " + (char)('A' + rng.Next(26)) + ".";
            if (r < 0.93) return Handles[rng.Next(Handles.Length)];
            return Handles[rng.Next(Handles.Length)] + rng.Next(10, 99);
        }
    }
}
