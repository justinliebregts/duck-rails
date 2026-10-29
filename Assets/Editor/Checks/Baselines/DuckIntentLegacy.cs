// Frozen pre-optimization oracle (c070582). Editor tests only; never included in players.
internal static class DuckIntentLegacy
{
    public static bool AcceptsSpeech(string text, bool nearbyBuddy, bool conversationOpen)
    {
        return !string.IsNullOrWhiteSpace(text) && (nearbyBuddy || conversationOpen
            || System.Text.RegularExpressions.Regex.IsMatch(text, @"\bduck\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase)
            || IsDirectRequest(Intent(text)) || IsSocialTrigger(Intent(text)));
    }

    static bool IsDirectRequest(string intent) => intent == "follow" || intent == "wait"
        || intent == "quiet" || intent == "dismiss" || intent == "acknowledge" || intent == "helicopter_directions";
    static bool IsSocialTrigger(string intent) => intent == "greeting" || intent == "goodbye" || intent == "laughter" || intent == "identity" || intent == "friendship" || intent == "favorite_movies" || intent == "favorite_music" || intent == "favorite_drinks" || intent == "appearance" || intent == "appearance_jealous";

    public static string Intent(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "unknown";
        string t = System.Text.RegularExpressions.Regex.Replace(text.ToLowerInvariant(), @"[-_\s]+", " ");
        if (Has(t, "go home", "go back", "go away", "stop follow", "stop following", "leave me alone", "give me space", "don't follow", "do not follow")) return "dismiss";
        if (Has(t, "shut up", "be quiet", "stop talking", "quiet please", "keep quiet", "stay quiet")) return "quiet";
        if (Has(t, "wait", "stay here")) return "wait";
        // Direction requests are verbal only; do not change the companion's destination.
        if (Has(t, "helicopter", "helicopters", "chopper", "supplies", "survival gear", "gear")
            && Has(t, "take me", "take us", "show me", "show us", "lead me", "lead us", "where is", "where's",
                "where the", "how do i get", "how do we get", "which way")) return "helicopter_directions";
        if (Has(t, "follow", "start following", "resume following", "stay with me", "stay with us",
            "stick with me", "stick with us", "come here", "come back", "come with", "come along")) return "follow";
        if (Has(t, "can you hear me", "do you hear me", "did you hear me", "are you listening",
            "can you hear us", "do you hear us", "did you hear that", "you there", "are you deaf",
            "mic check", "microphone check", "testing testing", "say something", "respond to me")) return "acknowledge";
        if (Has(t, "who are you", "who is duck", "what are you", "your name", "tell me about yourself",
            "introduce yourself", "why are you here", "why do you live here", "what are you doing here",
            "where do you live", "where are you from", "where do you come from", "why do you exist",
            "are you real", "what is your purpose", "what's your purpose", "what is your story", "what's your story")) return "identity";
        // Whole laugh tokens only: never match words such as "what" or "perhaps".
        if (System.Text.RegularExpressions.Regex.IsMatch(t, @"\b(?:(?:ha){1,8}|(?:he){2,8}|heh|hah|lol|lmao)\b")) return "laughter";
        // Explicit movement questions stay useful even when a landmark is named.
        if (Has(t, "climb", "climbing", "clamber", "get on the roof", "get onto the roof")) return "climbing";
        if (Has(t, "jump", "jumping")) return "jumping";
        if (IsJealousAppearanceComparison(t)) return "appearance_jealous";
        // Multiplayer appearance talk includes compliments and insults. Keep
        // explicit commands above this, and character backstory in its own topic.
        if (Has(t, "skin", "skins", "avatar", "avatars", "outfit", "outfits", "costume", "costumes",
            "your appearance", "your looks", "your feathers", "your face", "you look ugly", "you're ugly",
            "you are ugly", "you look cute", "you're cute", "you are cute", "you look handsome")
            || (Has(t, "character", "characters") && Has(t, "your", "duck", "player", "players", "ugly", "cute", "looks", "look")))
            return "appearance";
        // Personal drink preferences precede generic food and childhood-place topics.
        if (Has(t, "drink", "drinks", "beverage", "beverages", "tea", "coffee", "juice", "soda", "lemonade", "hot chocolate", "milkshake", "milkshakes")
            && Has(t, "favorite", "favorites", "favourite", "favourites", "like", "likes", "love", "loves",
                "enjoy", "enjoyed", "prefer", "best", "recommend", "grew up", "growing up", "childhood",
                "what do you drink", "what did you drink", "what would you drink")) return "favorite_drinks";
        // Personal music questions precede place memories and radio equipment advice.
        if (Has(t, "music", "song", "songs", "tune", "tunes", "band", "bands", "singer", "singers")
            && Has(t, "favorite", "favorites", "favourite", "favourites", "like", "likes", "love", "loves",
                "enjoy", "enjoyed", "prefer", "best", "recommend", "listen", "listened", "listening",
                "grew up", "growing up", "childhood")) return "favorite_music";
        // Specific ambient subjects override general place/food memories, without
        // claiming a sound is playing now. Commands and movement requests stay first.
        if (Has(t, "thunder", "thunderstorm", "thunderstorms", "lightning", "storm", "storms", "power outage",
            "power cut", "power cuts", "blackout", "blackouts", "power went out", "power is out", "lights went out", "no electricity")) return "thunder";
        if (Has(t, "crow", "crows", "caw", "cawing", "caw caw", "raven", "ravens")) return "crows";
        if (Has(t, "banging", "pounding", "clanging", "clanking", "metal banging", "factory noise", "factory noises", "factory sound", "factory sounds")
            || (Has(t, "factory", "factories", "machinery") && Has(t, "loud", "noise", "noisy", "bang", "bangs", "racket", "knocking"))) return "factory_banging";
        if (Has(t, "military", "army", "soldier", "soldiers", "troops", "gun", "guns", "gunfire", "gunshots", "gunshot",
            "shooting", "rifle", "rifles", "machine gun", "machine guns", "artillery", "distant shots")) return "military_sounds";
        // Named memories precede generic equipment words (cave bats, bags of
        // marshmallows, or a childhood hero must not become inventory advice).
        if (Has(t, "cave", "caves", "cavern", "caverns", "the mine", "old mine", "abandoned mine", "mineshaft", "mines", "mining cave", "mine cart", "mining tunnel")) return "caves";
        // Film preferences get their own answer rather than generic cinema memories.
        if (Has(t, "movie", "movies", "film", "films")
            && Has(t, "favorite", "favorites", "favourite", "favourites", "like", "likes", "love", "loves",
                "enjoy", "enjoyed", "prefer", "best", "recommend", "watched", "seen", "do you watch", "did you watch")) return "favorite_movies";
        if (Has(t, "cinema", "cinemas", "movie theater", "movie theatre", "movie house", "movies", "theater", "theatre")) return "cinema";
        if (Has(t, "apartment", "apartments", "apartment block", "apartment building", "flats")) return "apartments";
        if (Has(t, "warehouse", "warehouses", "storage depot", "loading dock")) return "warehouse";
        if (Has(t, "subway", "subways", "subway station", "metro", "underground station", "train station", "train platform")) return "subway";
        if (Has(t, "sheriff", "sheriffs", "cop", "cops", "police", "police station", "sheriff office", "sheriff's office", "holding cell")) return "sheriff";
        if (Has(t, "gas station", "gas stations", "petrol station", "filling station", "fuel station", "gas pumps", "friend's uncle", "friends uncle")) return "gas_station";
        if (Has(t, "church", "churches", "chapel", "bell", "bells", "bell tower", "pews")) return "church";
        if (Has(t, "barn", "barns", "farm", "farmhouse", "farm house", "farm animals", "barn animals", "hayloft", "hay loft", "chicken coop")) return "barn";
        if (Has(t, "creek", "bridge", "bridges", "aunt", "river", "riverbank", "river bank", "swimming", "swim", "paddling")) return "river";
        if (Has(t, "woods", "forest", "woodland", "hike", "hiking", "trees", "camping", "campfire", "cabin", "cabins", "s'mores", "smores", "marshmallow", "marshmallows", "marsh mellows")) return "woods";
        if (Has(t, "rambro", "ram bro", "rambo") && Has(t, "hero", "childhood", "kid", "grew up")) return "rambro_memories";
        if (Has(t, "helicopter", "helicopters", "chopper") && Has(t, "remote", "childhood", "kid", "used to")) return "helicopter_memories";
        if (Has(t, "school bus", "schoolbus", "bus", "buses", "bus stop", "bus ride")) return "bus_memories";
        if (Has(t, "truck", "trucks", "pickup truck", "delivery truck", "lorry", "lorries")) return "truck_memories";
        if (Has(t, "school", "classroom", "classrooms", "teacher", "homework", "schoolhouse", "school house", "playground", "recess", "detention")) return "school";
        if (Has(t, "factory", "factories", "old job", "used to work", "where did you work", "assembly line")) return "factory";
        if (Has(t, "store", "shop", "shopping", "groceries", "general store", "grocery store", "supermarket")) return "store";
        if (Has(t, "diner", "restaurant", "cafe", "coffee shop")) return "diner";
        if (Has(t, "water tower", "watertower", "water tank")) return "water_tower";
        if (Has(t, "helipad", "heli pad", "landing pad", "helicopter pad", "landing zone")) return "helipad_memories";
        if (Has(t, "city", "cities")) return "city";
        if (Has(t, "town", "quackville", "hometown", "town square", "main street")) return "town";
        if (Has(t, "creepy", "imposter", "impostor", "serial killer", "forever buddy", "watching people sleep",
            "watch me sleep", "smell of flesh", "secret admirer", "secretly in love")) return "creepy";
        if (Has(t, "friend", "friends", "friendship", "buddy", "buddies", "bestie", "befriend")) return "friendship";
        // Specific subjects precede generic combat, shopping and joke words.
        if (Has(t, "helicopter", "helicopters", "chopper", "rotor", "rotors")) return "helicopter";
        if (Has(t, "nail bat", "nail bats", "nailed bat", "baseball bat", "bat", "bats")) return "nail_bat";
        if (Has(t, "fry pan", "frying pan", "frypan", "pan", "pans", "skillet")) return "fry_pan";
        if (Has(t, "night vision", "nightvision", "goggles")) return "night_vision";
        if (Has(t, "flashlight", "flashlights", "flash light", "torch")) return "flashlight";
        if (Has(t, "helmet", "helmets", "hard hat")) return "helmet";
        if (Has(t, "walkie talkie", "walkie", "walkies", "radio", "radios")) return "walkie_talkie";
        if (Has(t, "rambro", "ram bro", "rambo")) return "rambro_knife";
        if (Has(t, "ninja", "ninja knife")) return "ninja_knife";
        // Everyday food beats generic shopping, money and joke words. Explicit
        // equipment and childhood places above retain their contextual answers.
        if (Has(t, "food", "foods", "pizza", "pizzas", "burger", "burgers", "hamburger", "hamburgers",
            "cheeseburger", "cheeseburgers", "rice", "fries", "french fries", "chips", "potato", "potatoes",
            "pasta", "noodles", "spaghetti", "bread", "toast", "sandwich", "sandwiches", "taco", "tacos",
            "burrito", "burritos", "soup", "salad", "steak", "meat", "chicken", "beef", "pork", "bacon",
            "sausage", "sausages", "fish", "seafood", "sushi", "egg", "eggs", "cheese", "vegetables", "fruit",
            "apple", "apples", "banana", "bananas", "cake", "cookies", "cookie", "ice cream", "chocolate",
            "candy", "dessert", "snack", "snacks", "breakfast", "lunch", "dinner", "supper", "meal", "meals",
            "hungry", "hunger", "eat", "eating", "ate", "cooking", "cook", "recipe", "recipes",
            "delicious", "tasty", "flavor", "flavour", "duck flesh", "duck meat")) return "creepy_food";
        if (Has(t, "gold", "coin", "coins", "money", "deposit")) return "gold";
        if (Has(t, "bag", "bags", "backpack", "backpacks", "carry", "inventory", "storage")) return "bags";
        // General supply questions point to the helicopter; named items and places win above.
        if (Has(t, "supplies", "supply", "survival gear", "gear")) return "helicopter";
        if (Has(t, "school", "classroom", "teacher", "homework")) return "school";
        if (Has(t, "factory", "factories", "old job", "used to work", "where did you work")) return "factory";
        if (Has(t, "river", "swimming", "swim", "paddling")) return "river";
        if (Has(t, "woods", "forest", "hike", "hiking", "trees")) return "woods";
        if (Has(t, "city", "cities", "noisy", "noise")) return "city";
        if (Has(t, "town", "quackville", "hometown", "grew up", "grow up", "childhood", "quiet", "where are you from")) return "town";
        if (Has(t, "weapon", "weapons", "unlock", "unlocking", "buy", "purchase", "price", "knife", "knives")) return "weapons";
        if (Has(t, "store", "shop", "shopping", "groceries")) return "store";
        if (Has(t, "rich", "wealth", "fortune", "treasure")) return "gold";
        if (Has(t, "died", "dead", "respawn", "killed")) return "death";
        if (Has(t, "staying alive", "stay alive", "survive", "survival", "safe", "safety", "tips")) return "survival";
        if (Has(t, "zombie", "zombies", "danger", "behind", "run")) return "danger";
        if (Has(t, "climb", "climbing", "roof", "rooftop", "stairs", "vehicle", "car", "truck", "bus")) return "climbing";
        if (Has(t, "jump", "gap")) return "jumping";
        if (Has(t, "again", "repeat", "what was that", "tell me more", "go on", "yes please")) return "repeat";
        if (Has(t, "joke", "funny", "laugh")) return "jokes";
        if (Has(t, "thank", "thanks", "good duck", "nice", "love you")) return "thanks";
        if (Has(t, "stupid", "hate", "useless", "bad duck")) return "kindness";
        if (Has(t, "who are", "your name", "what are you")) return "identity";
        if (Has(t, "how are", "happy", "feeling")) return "mood";
        if (Has(t, "bye", "goodbye", "see you")) return "goodbye";
        if (Has(t, "hello", "hey", "hi", "howdy")) return "greeting";
        return "unknown";
    }

    static bool IsJealousAppearanceComparison(string text)
    {
        const string comparison = @"(?:cuter|prettier|more handsome|more attractive|better looking|better(?: looking)? (?:skin|avatar|outfit|costume))";
        // Require an appearance comparison aimed at Duck, not "better weapon"
        // or a compliment saying Duck looks better than somebody else.
        if (System.Text.RegularExpressions.Regex.IsMatch(text,
            @"\b(?:not|isn't|aren't|isnt|arent)\s+(?:\w+\s+){0,2}" + comparison + @"\s+than\s+(?:you|duck)\b")) return false;
        return System.Text.RegularExpressions.Regex.IsMatch(text,
            @"\b" + comparison + @"\s+than\s+(?:you|duck|your (?:skin|avatar|outfit|costume))\b")
            || System.Text.RegularExpressions.Regex.IsMatch(text,
                @"\b(?:his|her|their|that player's|that players) (?:skin|avatar|outfit|costume) (?:is|looks) (?:better|nicer|cuter) than (?:yours|duck's|ducks)\b");
    }

    static bool Has(string text, params string[] words)
    {
        foreach (string word in words)
            if (System.Text.RegularExpressions.Regex.IsMatch(text,
                @"\b" + System.Text.RegularExpressions.Regex.Escape(word) + @"\b")) return true;
        return false;
    }

}
