using System;
using System.Collections.Generic;
using StereoKit;
using System.Net.Http;
using System.Threading.Tasks;

namespace flashcards;

class Program
{
    static void Main(string[] args)
    {
        // Initialize StereoKit
        SKSettings settings = new SKSettings
        {
            appName = "flashcards",
            assetsFolder = "Assets",
        };
        if (!SK.Initialize(settings))
            return;


        // fancy floor shader
        Matrix floorTransform = Matrix.TS(0, -1.5f, 0, new Vec3(30, 0.1f, 30));
        Material floorMaterial = new Material("floor.hlsl");
        floorMaterial.Transparency = Transparency.Blend;


        List<Deck> decks = new List<Deck>();
        Deck latin_deck = new Deck("latin", "11C9z5Hs");
        decks.Add(latin_deck);

        /*
        Plan for the code

        Flashcard spawns at location, you grab it and can think on it.

        You turn the flashcard around to see the back.

        Throw it upwards and it counts as correct

        Throw it downwards and it counts as wrong.

        New flashcard spawns at location.
        */

        Pose windowPose = new Pose(0.4f, 0f, -0.3f);
        windowPose.orientation = Quat.FromAngles(0, 180f, 0);

        Deck current_deck = latin_deck;
        Card current_card = current_deck.SelectCard();

        Pose cardPose = new Pose(0, 0, -0.5f);

        bool lastGrabbed = false;

        Vec3 lastHandPos = Vec3.Zero;

        Vec3 v1 = new();
        Vec3 v2 = new();
        Vec3 v3 = new();
        Vec3 v4 = new();

        Sound yes_sound = Sound.FromFile("yes.mp3");
        Sound no_sound = Sound.FromFile("no.mp3");

        string pastebinBit = "";
        string newDeckName = "";

        // Core application loop
        SK.Run(() =>
        {

            if (Device.DisplayBlend == DisplayBlend.Opaque)
                Mesh.Cube.Draw(floorMaterial, floorTransform);


            {

                UI.WindowBegin("Deck Selection", ref windowPose);
                Vec2 inputSize = V.XY(20 * U.cm, 0);
                Vec2 labelSize = V.XY(8 * U.cm, 0);
                // Text input for pastebin to add new decks.
                UI.Label("PasteBin End Url Bit: ", labelSize); UI.SameLine(); UI.Input("PasteBin", ref pastebinBit, inputSize, TextContext.Uri);
                UI.Label("New Deck Name: ", labelSize); UI.SameLine(); UI.Input("DeckName", ref newDeckName, inputSize, TextContext.Text);

                if (UI.Button("Add Deck"))
                {
                    decks.Add(new Deck(newDeckName, pastebinBit));
                }

                UI.HSeparator();

                // Selector for which deck the user has activated
                foreach (Deck deck in decks)
                {
                    if (UI.Button(deck.name))
                    {
                        current_deck = deck;
                        current_card = deck.SelectCard();
                    }
                }

                UI.WindowEnd();
            }

            Pose lastPose = cardPose;

            // This is where it gets complicated.
            // In order to maintain visual consistency we have to determine which side of the card is longer
            // We do this because StereoKit is an immediate mode UI so it calculates it's sizing on the fly.
            // Stereokit Panels are backface culled but their text isn't, so we have to artifically extend
            // the size of the panel on the shorter side to match the size of the panel on the larger side for
            // visual consistency and to hide the backwards text on the other side.

            // Determining which side is larger
            bool back_larger = true;
            Vec2 size = Text.Size(current_card.back);
            if (Text.Size(current_card.front).Length > size.Length)
            {
                back_larger = false;
                size = Text.Size(current_card.front);
            }

            Vec3 layout_pos;

            // This is the actual handle for the ui
            bool grabbed = UI.HandleBegin("card", ref cardPose, new Bounds(1f, 1f, 0.1f));

            UI.PushGrabAura(true);
            // This code is duplicated but it's liable to change and would be messier
            // to try to pull it out into an abstraction
            if (back_larger)
            {

                // back side
                var pose = new Pose(-0.15f, 0, 0);
                pose.orientation = Quat.FromAngles(0f, 180f, 0f);
                UI.PushSurface(pose);
                UI.LayoutPush(new Vec3(), new Vec2(0.15f, 1.0f));
                UI.LayoutArea(new Vec3(), new Vec2(0.15f, 0.4f));
                UI.PanelBegin();
                UI.Text(current_card.back, textAlign: TextAlign.Center);
                UI.PanelEnd();
                layout_pos = UI.LayoutAt;
                UI.LayoutPop();
                UI.PopSurface();
                // front side
                UI.LayoutPush(new Vec3(), new Vec2(0.15f, 1.0f));
                UI.LayoutArea(new Vec3(), new Vec2(0.15f, 0.4f));
                UI.PanelBegin();
                UI.VSpace((Math.Abs(layout_pos.y) - size.y * 4) / 2);
                UI.Text(" ");
                UI.Text(current_card.front, textAlign: TextAlign.Center);
                UI.VSpace((Math.Abs(layout_pos.y) - size.y * 4) / 2);
                UI.Text(" ");
                UI.PanelEnd();
                UI.LayoutPop();
            }
            else
            {
                // front side
                UI.LayoutPush(new Vec3(), new Vec2(0.15f, 1.0f));
                UI.LayoutArea(new Vec3(), new Vec2(0.15f, 0.4f));
                UI.PanelBegin();
                UI.Text(current_card.front, textAlign: TextAlign.Center);
                UI.PanelEnd();
                layout_pos = UI.LayoutAt;
                UI.LayoutPop();
                // back side
                var pose = new Pose(-0.15f, 0, 0);
                pose.orientation = Quat.FromAngles(0f, 180f, 0f);
                UI.PushSurface(pose);
                UI.LayoutPush(new Vec3(), new Vec2(0.15f, 1.0f));
                UI.LayoutArea(new Vec3(), new Vec2(0.15f, 0.4f));
                UI.PanelBegin();
                UI.VSpace((Math.Abs(layout_pos.y) - size.y * 4) / 2);
                UI.Text(" ");
                UI.Text(current_card.back, textAlign: TextAlign.Center);
                UI.VSpace((Math.Abs(layout_pos.y) - size.y * 4) / 2);
                UI.Text(" ");
                UI.PanelEnd();
                UI.LayoutPop();
                UI.PopSurface();
            }




            UI.PopGrabAura();
            UI.HandleEnd();

            Vec3 currentHandPos = cardPose.position;

            Vec3 displacement = currentHandPos - lastHandPos;
            var time = Time.Totalf;
            // We scale it by time to find the actual velocity
            Vec3 velocity = (displacement / time) * 1000f;

            v1 = v2;
            v2 = v3;
            v3 = v4;
            v4 = velocity;

            // We average the velocity of the last 4 frames together to get a more accurate result.
            Vec3 averageVelocity = (v1 + v2 + v3 + v4) / 4;

            lastHandPos = currentHandPos;

            const float VELOCITY_THRESHOLD = 0.3f;

            if (!grabbed && lastGrabbed)
            {
                Console.WriteLine("just ungrabbed");
                Console.WriteLine(averageVelocity);

                bool threshold_reached = true;
                if (averageVelocity.y >= VELOCITY_THRESHOLD)
                {
                    current_card.score += 1;
                    yes_sound.Play(cardPose.position);
                    cardPose.position.y -= 0.1f;
                    Console.WriteLine("Correct");
                }
                else if (averageVelocity.y <= -VELOCITY_THRESHOLD)
                {
                    current_card.score -= 1;
                    no_sound.Play(cardPose.position);
                    cardPose.position.y += 0.1f;
                    Console.WriteLine("Incorrect");
                }
                else
                {
                    threshold_reached = false;
                }

                if (threshold_reached)
                {
                    cardPose.orientation = Input.Head.orientation;
                    current_card = current_deck.SelectCard(current_card);
                }
            }

            lastGrabbed = grabbed;

        });
    }
}

class Deck
{
    public string name;
    public List<Card> cards = new List<Card>();
    private Random random = new Random();

    public Deck(string name)
    {
        this.name = name;
    }

    public Deck(string name, string pastebinBit) : this(name)
    {
        // We really shouldn't block on an async task like this in a VR application,
        // but I need to research how to manually poll async tasks like in Rust.
        // Another option is multithreading but we only have 3 threads on the quest 2.
        // So last resort shove it in a thread TODO.
        string pastebinContent = PastebinReader.ReadPastebinAsync(pastebinBit).ConfigureAwait(false).GetAwaiter().GetResult();

        string[] cards_strings = pastebinContent.Split(";");
        foreach (string frontnback in cards_strings)
        {
            // weird artifact of Quizlet export, we have an extra `;` at the end.
            if (frontnback == "")
            {
                break;
            }
            // Unable to do variable shadowing in C# 😔
            // Front and back of card are delimited by ' - '.
            string[] front_and_back = frontnback.Split(" - ");
            var front = front_and_back[0];
            var back = front_and_back[1];
            Card card = new Card(front, back);
            this.cards.Add(card);
        }
    }

    /// Use cardToIgnore to prevent re-doing the same card twice in a row
    public Card SelectCard(Card cardToIgnore)
    {
        Card retCard = SelectCard();
        while (retCard.Equals(cardToIgnore))
        {
            retCard = SelectCard();
        }
        return retCard;
    }
    // This is a terrible hacked together algorthim that gives a sembalince of spaced repitition.
    public Card SelectCard()
    {
        while (true)
        {
            int index = random.Next(cards.Count);
            Card potential_card = cards[index];
            if (potential_card.score > 0)
            {
                // The more times you have correctly gotten a card right, the less likely
                // it will be to be the next card.
                if (random.Next(potential_card.score) < 5)
                {
                    return potential_card;
                }
            }
            else
            {
                // Meanwhile, if you got a card wrong more often than right we just give that card
                return potential_card;
            }
        }
    }
}

class Card
{
    public string front;
    public string back;
    // score is negative if there if user has gotten the answer wrong more often than right, and vice versa.
    public int score;
    public Card(string front, string back)
    {
        this.front = front;
        this.back = back;
        this.score = 0;
    }
}

public class PastebinReader
{
    public static async Task<string> ReadPastebinAsync(string pasteId)
    {
        string baseUrl = "https://pastebin.com/raw/";
        string fullUrl = baseUrl + pasteId;

        using (var client = new HttpClient())
        {
            try
            {
                string content = await client.GetStringAsync(fullUrl);
                return content;
            }
            catch (HttpRequestException e)
            {
                Console.WriteLine($"Error fetching Pastebin content: {e.Message}");
                // null! Here be dragons.
                return null;
            }
        }
    }
}
