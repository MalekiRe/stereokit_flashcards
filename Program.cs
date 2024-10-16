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
        string pastebinContent = PastebinReader.ReadPastebinAsync("11C9z5Hs").ConfigureAwait(false).GetAwaiter().GetResult();


        // Initialize StereoKit
        SKSettings settings = new SKSettings
        {
            appName = "flashcards",
            assetsFolder = "Assets",
        };
        if (!SK.Initialize(settings))
            return;



        // Create assets used by the app
        Pose cubePose = new Pose(0, 0, -0.5f);
        Model cube = Model.FromMesh(
            Mesh.GenerateRoundedCube(Vec3.One * 0.1f, 0.02f),
            Material.UI);

        Matrix floorTransform = Matrix.TS(0, -1.5f, 0, new Vec3(30, 0.1f, 30));
        Material floorMaterial = new Material("floor.hlsl");
        floorMaterial.Transparency = Transparency.Blend;


        List<Deck> decks = new List<Deck>();
        Deck latin_deck = new Deck("latin");

        string[] items = pastebinContent.Split(";");
        foreach (string item in items)
        {
            if (item == "")
            {
                break;
            }
            string[] front_and_back = item.Split(" - ");
            var front = front_and_back[0];
            var back = front_and_back[1];
            Card card = new Card(front, back);
            latin_deck.cards.Add(card);
        }
        decks.Add(latin_deck);



        /*

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

        Vec3 lastHandPos = new Vec3(0, 0, 0);

        Vec3 v1 = new Vec3();
        Vec3 v2 = new Vec3();
        Vec3 v3 = new Vec3();
        Vec3 v4 = new Vec3();

        Sound yes_sound = Sound.FromFile("yes.mp3");
        Sound no_sound = Sound.FromFile("no.mp3");

        string pastebinBit = "";
        string newDeckName = "";

        // Core application loop
        SK.Run(() =>
        {

            if (Device.DisplayBlend == DisplayBlend.Opaque)
                Mesh.Cube.Draw(floorMaterial, floorTransform);


            UI.WindowBegin("Deck Selection", ref windowPose);
            Vec2 inputSize = V.XY(20 * U.cm, 0);
            Vec2 labelSize = V.XY(8 * U.cm, 0);
            UI.Label("PasteBin End Url Bit: ", labelSize);
            UI.SameLine();
            UI.Input("PasteBin", ref pastebinBit, inputSize, TextContext.Uri);
            UI.Label("New Deck Name: ", labelSize);
            UI.SameLine();
            UI.Input("DeckName", ref newDeckName, inputSize, TextContext.Text);

            if (UI.Button("Add Deck"))
            {
                string pastebinContent = PastebinReader.ReadPastebinAsync(pastebinBit).ConfigureAwait(false).GetAwaiter().GetResult();
                Deck newDeck = new Deck(newDeckName);

                string[] items = pastebinContent.Split(";");
                foreach (string item in items)
                {
                    if (item == "")
                    {
                        break;
                    }
                    string[] front_and_back = item.Split(" - ");
                    var front = front_and_back[0];
                    var back = front_and_back[1];
                    Card card = new Card(front, back);
                    newDeck.cards.Add(card);
                }
                decks.Add(newDeck);
            }

            UI.HSeparator();

            foreach (Deck deck in decks)
            {
                if (UI.Button(deck.name))
                {
                    current_deck = deck;
                    current_card = deck.SelectCard();
                }
            }

            UI.WindowEnd();
            Pose lastPose = cardPose;

            bool back_larger = true;
            Vec2 size = Text.Size(current_card.back);
            if (Text.Size(current_card.front).Length > size.Length)
            {
                back_larger = false;
                size = Text.Size(current_card.front);
            }

            Vec3 layout_pos;

            bool grabbed = UI.HandleBegin("card", ref cardPose, new Bounds(1f, 1f, 0.1f));

            UI.PushGrabAura(true);
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

            Vec3 dv = currentHandPos - lastHandPos;
            var time = (float)Time.Total;
            Vec3 velcoity = (dv / time) * 1000f;

            v1 = v2;
            v2 = v3;
            v3 = v4;
            v4 = velcoity;

            Vec3 average = (v1 + v2 + v3 + v4) / 4;

            lastHandPos = currentHandPos;

            if (!grabbed && lastGrabbed)
            {
                Console.WriteLine("just ungrabbed");
                Console.WriteLine(average);

                if (average.y >= 0.3)
                {
                    current_card.score += 1;
                    yes_sound.Play(cardPose.position);
                    cardPose.position.y -= 0.1f;
                    cardPose.orientation = Input.Head.orientation;
                    Console.WriteLine("Correct");
                    current_card = current_deck.SelectCard();
                }
                else if (average.y <= -0.3)
                {
                    current_card.score -= 1;
                    no_sound.Play(cardPose.position);
                    cardPose.position.y += 0.1f;
                    cardPose.orientation = Input.Head.orientation;
                    Console.WriteLine("Incorrect");
                    current_card = current_deck.SelectCard();
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

    // This is a hacked together algorthim that gives a sembalince of spaced repitition.
    public Card SelectCard()
    {
        while (true)
        {
            int index = random.Next(cards.Count);
            Card potential_card = cards[index];
            if (potential_card.score > 0)
            {
                if (random.Next(potential_card.score) < 5)
                {
                    return potential_card;
                }
            }
            else
            {
                return potential_card;
            }
        }
    }
}

class Card
{
    public string front;
    public string back;
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
                return null;
            }
        }
    }
}
