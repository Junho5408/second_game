using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Origami.Core.Economy;
using Origami.Core.Game;
using Origami.Core.Recognition;
using static Origami.Tests.Qa.WsDriver;

namespace Origami.Tests.Qa
{
    /// <summary>
    /// QA ORIGAMI-010: full 7-day scripted runs through <see cref="OrigamiGame"/> with literal expected daily money,
    /// settlement totals and endings. Expected values derived by QA by hand from CONTENT §5 (prices, popular table) and
    /// §9 / `balance_check.py econ` (967 plan, 437 no-books) — never from EndingRules or the snapshot itself (review W-2).
    /// </summary>
    [TestFixture]
    public class WorkshopEconomyRunQaTests
    {
        // A day plan: pages to buy in the morning (seated, book open), then the models to fold with Ref (one per sheet).
        private static void Day(OrigamiGame g, string[] buy, string[] folds)
        {
            Sit(g);
            foreach (var b in buy) Buy(g, b);
            foreach (var m in folds) Ref3(g, m);
        }

        private static string[] Five(string m) => new[] { m, m, m, m, m };

        private static readonly string[] None = new string[0];

        private static List<int> Run(OrigamiGame g, (string[] buy, string[] folds)[] days, int[] expectedTotals)
        {
            var money = new List<int>();
            for (int d = 0; d < days.Length; d++)
            {
                Assert.That(g.Snapshot.Day, Is.EqualTo(d + 1), "day number");
                Day(g, days[d].buy, days[d].folds);
                var st = Night(g);
                Assert.That(st.SettledDay, Is.EqualTo(d + 1), "settled day");
                if (expectedTotals != null) Assert.That(st.Total, Is.EqualTo(expectedTotals[d]), $"day {d + 1} sales total");
                money.Add(st.MoneyAfter);
            }
            return money;
        }

        [Test]
        public void BestStar3Plan_DailyMoney_77_152_277_312_502_687_967_EndingE04()
        {
            // CONTENT §9: dog bought day 2, cicada day 4; each day folds the best-paying owned model.
            // Day sales: 1 plane pop 13×5=65; 2 dog 27×5=135 (pop house); 3 dog 135 (pop tulip); 4 cicada 39×5=195 (pop house);
            // 5 dog pop 40×5=200; 6 cicada 195 (pop tulip); 7 cicada pop 58×5=290.
            var g = new OrigamiGame();
            var plan = new[]
            {
                (None, Five("plane")),
                (new[] { "dog" }, Five("dog")),
                (None, Five("dog")),
                (new[] { "cicada" }, Five("cicada")),
                (None, Five("dog")),
                (None, Five("cicada")),
                (None, Five("cicada")),
            };
            var money = Run(g, plan, new[] { 65, 135, 135, 195, 200, 195, 290 });
            CollectionAssert.AreEqual(new[] { 77, 152, 277, 312, 502, 687, 967 }, money);

            var s = g.Snapshot;
            Assert.That(s.IsEnded, Is.True);
            Assert.That(s.EndReason, Is.EqualTo(RunEndReason.Completed));
            Assert.That(s.Day, Is.EqualTo(7));
            Assert.That(s.Mode, Is.EqualTo(WorkshopMode.Ended));
            Assert.That(s.Money, Is.EqualTo(967));
            Assert.That(s.EndingId, Is.EqualTo("E-04"));
            Assert.That(s.TotalSales, Is.EqualTo(1215));
            Assert.That(s.TotalPaperCost, Is.EqualTo(68));   // 35 sheets, first free
            Assert.That(s.TotalBookCost, Is.EqualTo(200));
            Assert.That(s.FailureCount, Is.EqualTo(0));
            Assert.That(s.Completions.Get("plane", Grade.Star3), Is.EqualTo(5));
            Assert.That(s.Completions.Get("dog", Grade.Star3), Is.EqualTo(15));
            Assert.That(s.Completions.Get("cicada", Grade.Star3), Is.EqualTo(15));
            Assert.That(s.Completions.Total("house") + s.Completions.Total("tulip"), Is.EqualTo(0));
            CollectionAssert.AreEqual(new[] { "plane", "house", "dog", "cicada" }, s.OwnedModelIds);
        }

        [Test]
        public void NoBooks_AllStar3_DailyMoney_EndsAt437_EndingE05()
        {
            // Day 1 plane pop 65; days 2,4 house pop 18×5=90; days 3,5,6,7 house normal 12×5=60.
            var g = new OrigamiGame();
            var plan = new[]
            {
                (None, Five("plane")), (None, Five("house")), (None, Five("house")), (None, Five("house")),
                (None, Five("house")), (None, Five("house")), (None, Five("house")),
            };
            var money = Run(g, plan, new[] { 65, 90, 60, 90, 60, 60, 60 });
            CollectionAssert.AreEqual(new[] { 77, 157, 207, 287, 337, 387, 437 }, money);
            Assert.That(g.Snapshot.Money, Is.EqualTo(437));
            Assert.That(g.Snapshot.EndingId, Is.EqualTo("E-05"));
            Assert.That(g.Snapshot.TotalBookCost, Is.EqualTo(0));
        }

        [Test]
        public void TulipOnlyFromDay2_Star3_EndsAt627_EndingE02BeatsE04()
        {
            // Day 1 plane 65 → 77. Day 2 buy tulip (20); tulip normal 18×5=90, popular (days 3, 6) 27×5=135.
            var g = new OrigamiGame();
            var plan = new[]
            {
                (None, Five("plane")), (new[] { "tulip" }, Five("tulip")), (None, Five("tulip")), (None, Five("tulip")),
                (None, Five("tulip")), (None, Five("tulip")), (None, Five("tulip")),
            };
            var money = Run(g, plan, new[] { 65, 90, 135, 90, 90, 135, 90 });
            CollectionAssert.AreEqual(new[] { 77, 137, 262, 342, 422, 547, 627 }, money);
            Assert.That(g.Snapshot.Money, Is.EqualTo(627));
            Assert.That(g.Snapshot.Completions.Total("tulip"), Is.EqualTo(30));
            Assert.That(g.Snapshot.EndingId, Is.EqualTo("E-02"), "tulip garden has priority over tycoon (money 627 ≥ 600)");
        }

        [Test]
        public void AllFiveStar3_EndsAt900_EndingE03BeatsE04()
        {
            // Days 1-3 as the best plan (77, 152, 277). Day 4 (pop house): buy cicada 150 + tulip 20 → 107; house ★3 pop 18,
            // tulip ★3 normal 18, cicada ★3 39×3 = 117 → sales 153, money 107 − 10 + 153 = 250.
            // Days 5-7 cicada: 195, 195, 290 → 435, 620, 900.
            var g = new OrigamiGame();
            var plan = new[]
            {
                (None, Five("plane")),
                (new[] { "dog" }, Five("dog")),
                (None, Five("dog")),
                (new[] { "cicada", "tulip" }, new[] { "house", "tulip", "cicada", "cicada", "cicada" }),
                (None, Five("cicada")),
                (None, Five("cicada")),
                (None, Five("cicada")),
            };
            var money = Run(g, plan, new[] { 65, 135, 135, 153, 195, 195, 290 });
            CollectionAssert.AreEqual(new[] { 77, 152, 277, 250, 435, 620, 900 }, money);
            Assert.That(g.Snapshot.Money, Is.EqualTo(900));
            Assert.That(g.Snapshot.TotalBookCost, Is.EqualTo(220));
            Assert.That(g.Snapshot.EndingId, Is.EqualTo("E-03"), "master has priority over tycoon (900 ≥ 600)");
        }

        [Test]
        public void ShortestBankruptcy_Day4_Money0_Failures11_EndingE01()
        {
            // CONTENT §9 / F-001 AC-05: all sheets wasted. Day 1 end 12, day 2 end 2, day 3 one sheet → 0 → day 4 morning bankrupt.
            var g = new OrigamiGame();
            Sit(g);
            Times(5, () => Waste(g));
            Assert.That(g.Snapshot.Money, Is.EqualTo(12));
            Rej(g, x => x.TakeSheet(), RejectReason.NoSheetsLeft);
            Assert.That(Night(g).MoneyAfter, Is.EqualTo(12));
            Sit(g);
            Times(5, () => Waste(g));
            Assert.That(g.Snapshot.Money, Is.EqualTo(2));
            var st2 = Night(g);
            Assert.That(st2.EndReason, Is.EqualTo(RunEndReason.None), "money exactly 2 in the morning is not bankrupt");
            Assert.That(st2.NextPopularModelId, Is.EqualTo("tulip"));
            Assert.That(g.Snapshot.Day, Is.EqualTo(3));
            Sit(g);
            Waste(g);
            Assert.That(g.Snapshot.Money, Is.EqualTo(0));
            Rej(g, x => x.TakeSheet(), RejectReason.NotEnoughMoney);
            Assert.That(g.Snapshot.SheetsLeft, Is.EqualTo(4));
            var st = Night(g);

            Assert.That(st.SettledDay, Is.EqualTo(3));
            Assert.That(st.Total, Is.EqualTo(0));
            Assert.That(st.MoneyAfter, Is.EqualTo(0));
            Assert.That(st.EndReason, Is.EqualTo(RunEndReason.Bankrupt));
            Assert.That(st.NextPopularModelId, Is.Null);
            var s = g.Snapshot;
            Assert.That(s.EndReason, Is.EqualTo(RunEndReason.Bankrupt));
            Assert.That(s.Day, Is.EqualTo(4));
            Assert.That(s.Money, Is.EqualTo(0));
            Assert.That(s.FailureCount, Is.EqualTo(11));
            Assert.That(s.TotalPaperCost, Is.EqualTo(20));
            Assert.That(s.EndingId, Is.EqualTo("E-01"));
            Rej(g, x => x.Interact(Table), RejectReason.RunEnded);
        }

        [Test]
        public void BankruptWithMoney1_AndNoSheetsLeftBeforeNotEnoughMoney()
        {
            // Day 1: free plane ★3 (pop 13) + one wasted paid sheet → 18 → night 31. Day 2: buy tulip → 11, waste 5 → 1, sheets 0.
            var g = new OrigamiGame();
            Sit(g);
            Ref3(g, "plane");
            Waste(g);
            Assert.That(g.Snapshot.Money, Is.EqualTo(18));
            Assert.That(Night(g).MoneyAfter, Is.EqualTo(31));
            Sit(g);
            Buy(g, "tulip");
            Times(5, () => Waste(g));
            Assert.That(g.Snapshot.Money, Is.EqualTo(1));
            Assert.That(g.Snapshot.SheetsLeft, Is.EqualTo(0));
            Rej(g, x => x.TakeSheet(), RejectReason.NoSheetsLeft, "NoSheetsLeft is checked before NotEnoughMoney");
            var st = Night(g);
            Assert.That(st.EndReason, Is.EqualTo(RunEndReason.Bankrupt));
            Assert.That(g.Snapshot.Day, Is.EqualTo(3));
            Assert.That(g.Snapshot.Money, Is.EqualTo(1));
            Assert.That(g.Snapshot.EndingId, Is.EqualTo("E-01"));
        }

        [Test]
        public void ReviewW2_RichRunSequence_EndsAt161_EndingE05()
        {
            // The F001_AC09 sequence named in review W-2, with literal expectations:
            // F-011 AC-01/02 → day 3, money 152. Day 3: Waste (150), Var(0.06) house ★2 (148), buy tulip (128), Ref(tulip) (126).
            // Day 3 popular = tulip: house ★2 normal 8 + tulip ★3 popular 27 = 35 → 161. Days 4-7 sell nothing.
            var g = new OrigamiGame();
            Sit(g);
            Times(5, () => Ref3(g, "plane"));
            Assert.That(Night(g).MoneyAfter, Is.EqualTo(77));
            Sit(g);
            Buy(g, "dog");
            Assert.That(g.Snapshot.Money, Is.EqualTo(27));
            Times(5, () => Ref3(g, "dog"));
            Assert.That(Night(g).MoneyAfter, Is.EqualTo(152));
            Sit(g);
            Waste(g);
            var v = Var(g, 0.06);
            Assert.That(v.ModelId, Is.EqualTo("house"));
            Assert.That(v.Grade, Is.EqualTo(Grade.Star2));
            Buy(g, "tulip");
            Ref3(g, "tulip");
            Assert.That(g.Snapshot.Money, Is.EqualTo(126));
            var d3 = Night(g);
            Assert.That(d3.Lines.Select(l => (l.ModelId, l.Grade, l.Popular, l.Price)).ToArray(),
                Is.EqualTo(new[] { ("house", Grade.Star2, false, 8), ("tulip", Grade.Star3, true, 27) }));
            Assert.That(d3.MoneyAfter, Is.EqualTo(161));
            for (int d = 4; d <= 7; d++) Assert.That(Night(g).Total, Is.EqualTo(0), "day " + d);
            Assert.That(g.Snapshot.EndReason, Is.EqualTo(RunEndReason.Completed));
            Assert.That(g.Snapshot.Money, Is.EqualTo(161));
            Assert.That(g.Snapshot.FailureCount, Is.EqualTo(1));
            Assert.That(g.Snapshot.EndingId, Is.EqualTo("E-05"));
        }

        [Test]
        public void ReviewW2_BankruptRun_EndingIsLiteralE01()
        {
            var g = new OrigamiGame();
            Sit(g); Times(5, () => Waste(g)); Night(g);
            Sit(g); Times(5, () => Waste(g)); Night(g);
            Sit(g); Waste(g); Night(g);
            Assert.That(g.Snapshot.EndReason, Is.EqualTo(RunEndReason.Bankrupt));
            Assert.That(g.Snapshot.Money, Is.EqualTo(0));
            Assert.That(g.Snapshot.EndingId, Is.EqualTo("E-01"));
        }

        [Test]
        public void IdleSevenDays_EndsCompletedAt20_E01_AndDay7HasNoBankruptcyCheck()
        {
            var g = new OrigamiGame();
            for (int d = 1; d <= 6; d++)
            {
                var st = Night(g);
                Assert.That(st.EndReason, Is.EqualTo(RunEndReason.None));
                Assert.That(g.Snapshot.Day, Is.EqualTo(d + 1));
                Assert.That(g.Snapshot.SheetsLeft, Is.EqualTo(5));
                Assert.That(g.Snapshot.Mode, Is.EqualTo(WorkshopMode.Walking));
            }
            var last = Night(g);
            Assert.That(last.EndReason, Is.EqualTo(RunEndReason.Completed));
            Assert.That(last.NextPopularModelId, Is.Null);
            Assert.That(g.Snapshot.Day, Is.EqualTo(7));
            Assert.That(g.Snapshot.EndingId, Is.EqualTo("E-01"));
        }

        [Test]
        public void Day7_MoneyBelow2_IsCompletedNotBankrupt()
        {
            // Day 7 settlement ends the run as Completed even if money < 2 (no bankruptcy check after day 7).
            var g = new OrigamiGame();
            Times(6, () => Night(g));
            Sit(g);
            Buy(g, "tulip");                       // 20 → 0
            Ok(g, x => x.TakeSheet());             // first sheet still free on day 7
            Ok(g, x => x.Discard());
            Assert.That(g.Snapshot.Money, Is.EqualTo(0));
            var st = Night(g);
            Assert.That(st.EndReason, Is.EqualTo(RunEndReason.Completed));
            Assert.That(g.Snapshot.EndingId, Is.EqualTo("E-01"));
        }

        [Test]
        public void SettlementUsesTheSettledDaysPopularModel_AndDisplayOrder()
        {
            // Day 1 (pop plane): house ★3 12, plane ★3 pop 13, house ★1 (Var 0.15) 4. Order = completion order.
            var g = new OrigamiGame();
            Sit(g);
            Ref3(g, "house");
            Ref3(g, "plane");
            var v = Var(g, 0.15);
            Assert.That(v.Grade, Is.EqualTo(Grade.Star1));
            CollectionAssert.AreEqual(new[] { "house", "plane", "house" }, g.Snapshot.Display.Select(p => p.ModelId).ToArray());
            var st = Night(g);
            Assert.That(st.Lines.Select(l => l.Price).ToArray(), Is.EqualTo(new[] { 12, 13, 4 }));
            Assert.That(st.Lines.Select(l => l.Popular).ToArray(), Is.EqualTo(new[] { false, true, false }));
            Assert.That(st.Total, Is.EqualTo(29));
            Assert.That(st.MoneyAfter, Is.EqualTo(20 - 4 + 29));
            Assert.That(st.NextPopularModelId, Is.EqualTo("house"));
            Assert.That(g.Snapshot.PopularModelId, Is.EqualTo("house"));
            Assert.That(g.Snapshot.Display.Count, Is.EqualTo(0));
            Assert.That(g.Snapshot.TotalSales, Is.EqualTo(29));
        }

        [Test]
        public void SameScriptTwice_GivesIdenticalValues()
        {
            Func<OrigamiGame> play = () =>
            {
                var g = new OrigamiGame();
                Sit(g); Ref3(g, "plane"); Var(g, 0.06); Waste(g); Night(g);
                Sit(g); Buy(g, "tulip"); Ref3(g, "tulip"); Night(g);
                return g;
            };
            var a = play().Snapshot;
            var b = play().Snapshot;
            Assert.That(b.Money, Is.EqualTo(a.Money));
            Assert.That(b.Day, Is.EqualTo(a.Day));
            Assert.That(b.FailureCount, Is.EqualTo(a.FailureCount));
            Assert.That(b.Completions, Is.EqualTo(a.Completions));
            CollectionAssert.AreEqual(a.OwnedModelIds, b.OwnedModelIds);
            // Day 1: free plane, Var(0.06) −2, Waste −2 → 16; sells plane ★3 pop 13 + house ★2 normal 8 → 37.
            // Day 2 (pop house): tulip page −20, sheet −2 → 15; tulip ★3 normal 18 → 33.
            Assert.That(a.Money, Is.EqualTo(33));
        }
    }
}
