using System.Collections;
using System.Linq;
using Game.Core;
using Game.Gameplay;
using Game.Infrastructure;
using Game.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    public sealed class BootstrapSmokeTests
    {
        [UnityTest]
        public IEnumerator BootstrapScene_CreatesCompositionRoot()
        {
            var operation = SceneManager.LoadSceneAsync("Bootstrap", LoadSceneMode.Single);
            while (operation != null && !operation.isDone)
            {
                yield return null;
            }

            yield return null;
            var root = Object.FindFirstObjectByType<CompositionRoot>();
            Assert.That(root, Is.Not.Null);
            Assert.That(root.Context, Is.Not.Null);
            Assert.That(root.Context.Platform.IsAvailable, Is.False);

            var presenter = Object.FindFirstObjectByType<VerticalSlicePresenter>();
            Assert.That(presenter, Is.Not.Null);
            Assert.That(presenter.IsReady, Is.True, presenter.LastError);
            Assert.That(presenter.CurrentLocale, Is.EqualTo("zh-Hans"));
            Assert.That(presenter.CurrentPhase, Is.EqualTo(RunPhase.Map));
            Assert.That(presenter.VisibleActionCount, Is.EqualTo(1));
            Assert.That(presenter.SetLocale("en"), Is.True);
            Assert.That(presenter.CurrentLocale, Is.EqualTo("en"));
            Assert.That(presenter.InvokeVisibleAction(0), Is.True);
            yield return null;
            Assert.That(presenter.CurrentPhase, Is.EqualTo(RunPhase.Narrative));
            Assert.That(presenter.VisibleActionCount, Is.EqualTo(2));
            Assert.That(presenter.InvokeVisibleAction(0), Is.True);
            yield return null;
            Assert.That(presenter.CurrentPhase, Is.EqualTo(RunPhase.Map));
            Assert.That(presenter.VisibleActionCount, Is.EqualTo(2));
            Assert.That(presenter.InvokeVisibleAction(0), Is.True);
            yield return null;
            Assert.That(presenter.CurrentPhase, Is.EqualTo(RunPhase.Combat));
            Assert.That(presenter.VisibleActionCount, Is.GreaterThan(1));
            Assert.That(presenter.LastError, Is.Empty);
            Assert.That(presenter.ContainsVisibleText("Intent: Attack 4"), Is.True);
            Assert.That(presenter.Session.EndTurn().Succeeded, Is.True);
            presenter.Refresh();
            Assert.That(presenter.ContainsVisibleText("Apply Weak 1 to you"), Is.True);
            Assert.That(presenter.Session.EndTurn().Succeeded, Is.True);
            Assert.That(presenter.Session.Combat.Player.GetStatus(CombatStatusKind.Weak), Is.EqualTo(1));

            var exposeId = new ContentId("card.expose");
            for (var turn = 0; turn < 2 && !presenter.Session.Combat.Hand.Contains(exposeId); turn++)
            {
                Assert.That(presenter.Session.EndTurn().Succeeded, Is.True);
            }

            var exposeIndex = presenter.Session.Combat.Hand.ToList().IndexOf(exposeId);
            Assert.That(exposeIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(presenter.Session.PlayCard(exposeIndex, 0).Succeeded, Is.True);
            presenter.Refresh();
            Assert.That(presenter.ContainsVisibleText("Vulnerable 2"), Is.True);

            WinCurrentCombat(presenter.Session);
            Assert.That(presenter.Session.Phase, Is.EqualTo(RunPhase.Reward));
            Assert.That(presenter.Session.ClaimReward(presenter.Session.RewardCandidates[0]).Succeeded, Is.True);
            Assert.That(presenter.Session.SelectMapNode(new ContentId("route.interlude")).Succeeded, Is.True);
            Assert.That(presenter.Session.Choose(new ContentId("choice.enter_archive")).Succeeded, Is.True);
            Assert.That(presenter.Session.Relics, Does.Contain(new ContentId("relic.ember_quill")));
            Assert.That(presenter.Session.SelectMapNode(new ContentId("route.rest")).Succeeded, Is.True);
            var upgraded = false;
            for (var index = 0; index < presenter.Session.Deck.Count && !upgraded; index++)
            {
                upgraded = presenter.Session.UpgradeCard(index).Succeeded;
            }

            Assert.That(upgraded, Is.True);
            Assert.That(presenter.Session.SelectMapNode(new ContentId("route.shop")).Succeeded, Is.True);
            presenter.Refresh();
            Assert.That(presenter.ContainsVisibleText("Gold 15"), Is.True);
            Assert.That(presenter.Session.ShopOffers.Count, Is.EqualTo(3));
            Assert.That(presenter.Session.PurchaseShopCard(presenter.Session.ShopOffers[0]).Succeeded, Is.True);
            Assert.That(presenter.Session.Resources[new ContentId("resource.gold")], Is.EqualTo(3));
            Assert.That(presenter.Session.LeaveShop().Succeeded, Is.True);
            Assert.That(presenter.Session.SelectMapNode(new ContentId("route.warden")).Succeeded, Is.True);
            presenter.Refresh();
            Assert.That(presenter.ContainsVisibleText("Ember Quill"), Is.True);
            Assert.That(presenter.ContainsVisibleText("Strength 1"), Is.True);
            Assert.That(presenter.Session.Combat.Player.Block, Is.EqualTo(2));
        }

        private static void WinCurrentCombat(RunSession session)
        {
            for (var commandBudget = 0; commandBudget < 200 && session.Phase == RunPhase.Combat; commandBudget++)
            {
                var played = false;
                for (var index = 0; index < session.Combat.Hand.Count; index++)
                {
                    var target = session.Combat.Enemies.ToList().FindIndex(enemy => !enemy.IsDefeated);
                    if (session.PlayCard(index, target).Succeeded)
                    {
                        played = true;
                        break;
                    }
                }

                if (!played && session.Phase == RunPhase.Combat)
                {
                    Assert.That(session.EndTurn().Succeeded, Is.True);
                }
            }

            Assert.That(session.Phase, Is.EqualTo(RunPhase.Reward));
        }
    }
}
