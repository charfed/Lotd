﻿using Lotd.FileFormats;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

#pragma warning disable 649

namespace Lotd
{
    /// <summary>
    /// Helper class for loading YDK files (YGOPro)
    /// </summary>
    static class YdkHelper
    {
        static Dictionary<long, long> ydkIdToOfficialId = new Dictionary<long, long>();
        static Dictionary<long, long> officialIdToYdkId = new Dictionary<long, long>();
        const string idMapFile = "YdkIds.txt";

        /// <summary>
        /// File extension used by YGOPRO
        /// </summary>
        public const string FileExtension = ".ydk";

        public static MemTools.YdcDeck LoadDeck(string filePath)
        {
            // Loader is based on https://github.com/Fluorohydride/ygopro/blob/master/gframe/deck_manager.cpp

            MemTools.YdcDeck result = new MemTools.YdcDeck();
            result.DeckName = Path.GetFileNameWithoutExtension(filePath);
            result.MainDeck = new short[Constants.NumMainDeckCards];
            result.ExtraDeck = new short[Constants.NumMainDeckCards];
            result.SideDeck = new short[Constants.NumMainDeckCards];
            result.Unk1 = new byte[12];
            result.Unk2 = new byte[12];
            result.IsDeckComplete = true;
            result.DeckAvatarId = 5;// Joey! TODO: Allow this to be configured...
            if (File.Exists(filePath))
            {
                bool isSide = false;

                string[] lines = File.ReadAllLines(filePath);
                foreach (string line in lines)
                {
                    if (line.StartsWith("!"))
                    {
                        isSide = true;
                    }
                    else if (!line.StartsWith("#"))
                    {
                        long ydkCardId, officialCardId;
                        CardInfo card;
                        if (long.TryParse(line.Trim(), out ydkCardId) &&
                            ydkIdToOfficialId.TryGetValue(ydkCardId, out officialCardId) &&
                            Program.Manager.CardManager.Cards.TryGetValue((short)officialCardId, out card))
                        {
                            if (isSide)
                            {
                                if (result.NumSideDeckCards < result.SideDeck.Length)
                                {
                                    result.SideDeck[result.NumSideDeckCards] = card.CardId;
                                    result.NumSideDeckCards++;
                                }
                            }
                            else if (card.CardTypeFlags.HasFlag(CardTypeFlags.Fusion) ||
                                card.CardTypeFlags.HasFlag(CardTypeFlags.Synchro) ||
                                card.CardTypeFlags.HasFlag(CardTypeFlags.DarkSynchro) ||
                                card.CardTypeFlags.HasFlag(CardTypeFlags.Xyz) ||
                                card.CardTypeFlags.HasFlag(CardTypeFlags.Link))
                            {
                                if (result.NumExtraDeckCards < result.ExtraDeck.Length)
                                {
                                    result.ExtraDeck[result.NumExtraDeckCards] = card.CardId;
                                    result.NumExtraDeckCards++;
                                }
                            }
                            else
                            {
                                if (result.NumMainDeckCards < result.MainDeck.Length)
                                {
                                    result.MainDeck[result.NumMainDeckCards] = card.CardId;
                                    result.NumMainDeckCards++;
                                }
                            }
                        }
                    }
                }
            }
            return result;
        }

        public static void SaveDeck(MemTools.YdcDeck deck, string path)
        {
            try
            {
                using (TextWriter writer = File.CreateText(path))
                {
                    writer.WriteLine("#main");
                    for (int i = 0; i < deck.NumMainDeckCards; i++)
                    {
                        long ydkCardId;
                        if (officialIdToYdkId.TryGetValue(deck.MainDeck[i], out ydkCardId))
                        {
                            writer.WriteLine(ydkCardId);
                        }
                    }
                    writer.WriteLine();

                    writer.WriteLine("#extra");
                    for (int i = 0; i < deck.NumExtraDeckCards; i++)
                    {
                        long ydkCardId;
                        if (officialIdToYdkId.TryGetValue(deck.ExtraDeck[i], out ydkCardId))
                        {
                            writer.WriteLine(ydkCardId);
                        }
                    }
                    writer.WriteLine();

                    writer.WriteLine("!side");
                    for (int i = 0; i < deck.NumSideDeckCards; i++)
                    {
                        long ydkCardId;
                        if (officialIdToYdkId.TryGetValue(deck.SideDeck[i], out ydkCardId))
                        {
                            writer.WriteLine(ydkCardId);
                        }
                    }
                    writer.WriteLine();
                }
            }
            catch
            {
            }
        }

        public static void LoadIdMap()
        {
            ydkIdToOfficialId.Clear();
            officialIdToYdkId.Clear();

            if (File.Exists(idMapFile))
            {
                string[] lines = File.ReadAllLines(idMapFile);
                foreach (string line in lines)
                {
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        string[] splitted = line.Split();
                        if (splitted.Length >= 2)
                        {
                            long ydkId, officialId;
                            if (long.TryParse(splitted[0], out ydkId) &&
                                long.TryParse(splitted[1], out officialId))
                            {
                                ydkIdToOfficialId[ydkId] = officialId;
                                officialIdToYdkId[officialId] = ydkId;
                            }
                        }
                    }
                }
            }

            if (ydkIdToOfficialId.Count > 0)
            {
                ValidateCardIds();
            }
        }

        public static void GenerateIdMap()
        {
            ydkIdToOfficialId.Clear();
            officialIdToYdkId.Clear();

            Dictionary<string, CardInfo> alternativeCardNames = new Dictionary<string, CardInfo>();
            foreach (CardInfo card in Program.Manager.CardManager.Cards.Values)
            {
                string name = card.Name.English;
                bool alternativeName = false;
                if (name.Contains("#"))
                {
                    name = name.Replace("#", string.Empty);
                    alternativeName = true;
                }
                if (name.Contains("・"))
                {
                    name = name.Replace("・", string.Empty);
                    alternativeName = true;
                }
                if (name.Contains("β"))
                {
                    name = name.Replace("β", "B");
                    alternativeName = true;
                }
                if (name.Contains("α"))
                {
                    name = name.Replace("α", "Alpha");
                    alternativeName = true;
                }
                if (name.Contains("The"))
                {
                    name = name.Replace("The", "the");
                    alternativeName = true;
                }
                if (alternativeName)
                {
                    alternativeCardNames[name] = card;
                }
            }
            // Manually fix a few others
            alternativeCardNames["Necrolancer the Time-lord"] = Program.Manager.CardManager.Cards[4149];//Necrolancer the Timelord
            alternativeCardNames["LaLa Li-Oon"] = Program.Manager.CardManager.Cards[4197];//LaLa Li-oon
            alternativeCardNames["Master  Expert"] = Program.Manager.CardManager.Cards[4254];//Master & Expert
            alternativeCardNames["Man-Eating Black Shark"] = Program.Manager.CardManager.Cards[4571];//Man-eating Black Shark
            alternativeCardNames["Muko"] = Program.Manager.CardManager.Cards[5362];//Null and Void
            alternativeCardNames["After The Struggle"] = Program.Manager.CardManager.Cards[5394];//After the Struggle
            alternativeCardNames["Vampiric Orchis"] = Program.Manager.CardManager.Cards[5588];//Vampire Orchis
            alternativeCardNames["B.E.S. Big Core"] = Program.Manager.CardManager.Cards[6199];//Big Core
            alternativeCardNames["Supernatural Regeneration"] = Program.Manager.CardManager.Cards[8072];//Metaphysical Regeneration
            alternativeCardNames["Silent Graveyard"] = Program.Manager.CardManager.Cards[8835];//Forbidden Graveyard
            alternativeCardNames["Vampiric Koala"] = Program.Manager.CardManager.Cards[8858];//Vampire Koala

            using (WebClient client = new WebClient())
            {
                client.Proxy = null;
                string json = client.DownloadString("https://db.ygoprodeck.com/api/v4/cardinfo.php");
                YgoProCardJson[][] cardsArray = JsonSerializer<YgoProCardJson[][]>.Deserialize(json);
                if (cardsArray.Length > 0 && cardsArray[0].Length > 0)
                {
                    foreach (YgoProCardJson card in cardsArray[0])
                    {
                        if (!ydkIdToOfficialId.ContainsKey(card.id))
                        {
                            CardInfo cardInfo = Program.Manager.CardManager.FindCardByName(Language.English, card.name);
                            if (cardInfo == null)
                            {
                                alternativeCardNames.TryGetValue(card.name, out cardInfo);
                            }
                            if (cardInfo != null)
                            {
                                ydkIdToOfficialId[card.id] = cardInfo.CardId;
                                officialIdToYdkId[cardInfo.CardId] = card.id;
                            }
                        }
                    }
                }
            }

            using (TextWriter writer = File.CreateText(idMapFile))
            {
                foreach (KeyValuePair<long, long> cardId in ydkIdToOfficialId)
                {
                    writer.WriteLine(cardId.Key + " " + cardId.Value);
                }
            }

            if (ydkIdToOfficialId.Count > 0)
            {
                ValidateCardIds();
            }
        }

        private static void ValidateCardIds()
        {
            int numMissingCards = 0;
            foreach (CardInfo card in Program.Manager.CardManager.Cards.Values)
            {
                if (!officialIdToYdkId.ContainsKey(card.CardId) && card.CardId != 0)
                {
                    numMissingCards++;
                    Debug.WriteLine("Couldn't find YDK card '" + card.Name.English + "' (" + card.CardId + ")");
                }
            }
            if (numMissingCards > 0)
            {
                Debug.WriteLine("Failed to find " + numMissingCards + " YDK card ids");
            }
        }

        [DataContract]
        class YgoProCardJson
        {
            [DataMember]
            public long id;
            [DataMember]
            public string name;
        }

        static class JsonSerializer<T> where T : class
        {
            public static T Deserialize(string json)
            {
                using (MemoryStream stream = new MemoryStream(Encoding.Unicode.GetBytes(json)))
                {
                    DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(T));
                    return serializer.ReadObject(stream) as T;
                }
            }
        }
    }
}
