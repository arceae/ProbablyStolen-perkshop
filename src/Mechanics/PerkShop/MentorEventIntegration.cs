using System;
using System.Collections;
using System.Globalization;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace PerkShopFramework
{
    public sealed partial class PerkShopMod
    {
        private const string MentorIntroSeenKey = "MentorIntroSeen";
        private const string MentorPromptedLevelKey = "MentorPromptedLevel";
        private const string MentorNextVisitDayKey = "MentorNextVisitDay";
        private const string MentorVisitCountKey = "MentorVisitCount";
        private const string MentorNativeTutorialDoneKey = "MentorNativeTutorialDone";
        private const string PerkShopMentorClientId = "perkshop_mentor";

        private bool _mentorIntroSeen;
        private int _mentorPromptedLevel;
        private int _mentorNextVisitDay = -1;
        private int _mentorVisitCount;
        private bool _mentorNativeTutorialDone;
        private bool _mentorVisitQueued;
        private float _nextMentorAttemptTime;
        private int _mentorAdvanceQueuedFrame = -1;

        private void UpdateMentorEvents(PlayerStore store, bool includeDeferredVisits)
        {
            if (!CanUseInRun(store) || store.storeClientManager == null || StoreUIManager.Instance == null)
            {
                return;
            }

            if (Time.unscaledTime < _nextMentorAttemptTime || _mentorVisitQueued)
            {
                return;
            }

            var day = GetCurrentDaySafe();
            if (day <= 0)
            {
                return;
            }

            if (!_mentorIntroSeen && (_mentorNativeTutorialDone || !store.tutorialEnable || !store.enableStoryClient))
            {
                if (QueueMentorVisit(store, true, 1))
                {
                    _mentorIntroSeen = true;
                    WriteInt(store, MentorIntroSeenKey, 1);
                    _savePending = true;
                }

                return;
            }

            if (!includeDeferredVisits)
            {
                return;
            }

            if (_mentorNextVisitDay > 0 && day >= _mentorNextVisitDay && _pointsEarned > _mentorPromptedLevel)
            {
                QueueMentorVisit(store, false, _mentorVisitCount + 1);
            }
        }

        public void OnNativeTutorialMentorDone()
        {
            try
            {
                _mentorNativeTutorialDone = true;
                _nextMentorAttemptTime = 0f;

                var store = GetStore();
                if (CanUseInRun(store))
                {
                    WriteInt(store, MentorNativeTutorialDoneKey, 1);
                    _savePending = true;
                }

                PerkShopLog.Msg("Native tutorial mentor completed; perk shop mentor intro unlocked.");
            }
            catch (Exception ex)
            {
                PerkShopLog.Error("OnNativeTutorialMentorDone failed: " + ex);
            }
        }

        private static bool IsPerkShopMentorCurrent(PlayerStore store)
        {
            try
            {
                var instance = store != null ? store.currentClientInstance : null;
                var client = instance != null ? instance.storeClient : null;
                return client != null && string.Equals(client.identifier, PerkShopMentorClientId, StringComparison.Ordinal);
            }
            catch
            {
                return false;
            }
        }

        private IEnumerator TryAdvanceMentorAfterFrame(DialogUIManager dialogUi)
        {
            yield return new WaitForEndOfFrame();

            try
            {
                var store = GetStore();
                if (dialogUi == null || !CanUseInRun(store) || !IsPerkShopMentorCurrent(store))
                {
                    yield break;
                }

                if (!dialogUi.isWaitingForManualAdvance)
                {
                    PerkShopLog.Msg("Mentor advance fallback skipped: native input path already advanced.");
                    yield break;
                }

                var current = dialogUi.currentDialog;
                PerkShopLog.Msg("Mentor advance fallback invoked. next=" + (current != null && current.nextDialogue != null) + " waiting=" + dialogUi.isWaitingForManualAdvance);
                dialogUi.ContinueConversation();
            }
            catch (Exception ex)
            {
                PerkShopLog.Error("TryAdvanceMentorAfterFrame failed: " + ex);
            }
        }

        private bool QueueMentorVisit(PlayerStore store, bool intro, int visitNumber)
        {
            try
            {
                var manager = store.storeClientManager;
                if (manager == null)
                {
                    return false;
                }

                var client = StoreClientListStory.CreateMentorClient();
                if (client == null)
                {
                    PerkShopLog.Warning("Mentor event failed: CreateMentorClient returned null.");
                    return false;
                }

                client.identifier = PerkShopMentorClientId;
                client.cooldown = 0;
                client.mainDialogue = BuildMentorDialogue(store, intro, visitNumber);
                manager.AddNextClient(client);

                _mentorVisitQueued = true;
                _nextMentorAttemptTime = Time.unscaledTime + 2f;
                PerkShopLog.Msg("Queued perk mentor visit #" + visitNumber + " (" + (intro ? "intro" : "point") + "). day=" + GetCurrentDaySafe());
                return true;
            }
            catch (Exception ex)
            {
                _mentorVisitQueued = false;
                _nextMentorAttemptTime = Time.unscaledTime + 5f;
                PerkShopLog.Error("QueueMentorVisit failed: " + ex);
                return false;
            }
        }

        private Dialogue BuildMentorDialogue(PlayerStore store, bool intro, int visitNumber)
        {
            var speaker = LocHelper.GetLocalizedClientName("name_mentor");
            if (string.IsNullOrEmpty(speaker))
            {
                speaker = "导师";
            }

            string[] lines;
            if (intro)
            {
                lines = new[]
                {
                    "你看账本了吗？",
                    "我做这行时，可没人会停下来教你这些。买卖能赚信用点，也能磨出眼力。",
                    "眼力攒够了，就去配给站，交易台旁那块【体悟】。那里能把眼力换成本事。",
                    "换到手的，会一直留在你这份档里。不是今天有、明天没的玩意儿。",
                    "*他从夹克口袋里掏出一张便签，放在了柜台上。*",
                    "这是你要先记住的一件事。等你做够第一笔流水，就会知道自己看懂了什么。"
                };
            }
            else if (visitNumber <= 2)
            {
                lines = new[]
                {
                    "我以前也想过，流水漂亮，本事自然就会跟着长。",
                    "后来才发现不是这么回事。做过的买卖要回头看，才知道自己哪儿看走了眼。",
                    "你这段生意，已经够你再看出一层门道了。去配给站，交易台旁那个【体悟】，别把这点眼力留在脑子里。",
                    "我年轻时攒下过不少这样的东西，最后全被日子磨没了。换进手里的，才是真的。",
                    "再做够下一阶的买卖流水，你还会更进一步。差多少，账本比我清楚。"
                };
            }
            else if (visitNumber == 3)
            {
                lines = new[]
                {
                    "我以前在下层区跑货，见过不少人今天替治安部递话，明天又替革命军传信。最后两边都嫌他知道得太多。",
                    "在这种地方，站队不难，难的是站了队还活着。真想长久做生意，得让每一方都觉得你有用，又都别觉得你能被随手拿走。",
                    "你这段时间的买卖，已经让你看出点门道了。配给站，交易台旁的【体悟】，把这层眼力换进手里。",
                    "写规矩的人希望你只认一边。你别急着认，先学会怎么活。",
                    "再做够下一阶的流水，我还来。"
                };
            }
            else if (visitNumber == 4)
            {
                lines = new[]
                {
                    "我曾替黑市带过一批不能见光的货。买家是革命军，路上放行的是治安部的人，最后收货的却坐着上层区的车。",
                    "那笔生意教会我一件事：别问谁干净，先看谁能让你活到下一单。",
                    "你又攒出一层经验了。配给站，老地方。趁还看得清，换成本事。",
                    "别让任何一边摸清你的底。你越像一个有用、但不好拿的人，越没人愿意先动你。",
                    "下一阶的流水做够了，我还来。"
                };
            }
            else if (visitNumber == 5)
            {
                lines = new[]
                {
                    "我看过站队的人倒在地上，也看过两边都不得罪的人被当成软柿子。中立从来不是缩着脖子做人。",
                    "治安部给你路，不一定是保护你；革命军给你面子，也不一定是信你。黑市更简单，只认你手上有没有他们要的东西。",
                    "你这点眼力已经熟了。配给站，交易台旁那块牌子，换进手里。",
                    "别急着靠哪一边。先让他们都需要你，再谈信谁。",
                    "再做够下一阶的流水，我还会来。"
                };
            }
            else
            {
                lines = new[]
                {
                    "说实话，我以前不太看好你。",
                    "这地方的人不是急着找靠山，就是急着把谁踩下去。你在几拨人之间来回周旋，到现在居然还没把自己做成谁的刀。",
                    "能活下来的未必最聪明，但知道自己什么时候该装聋、什么时候该把牌摊开的人，通常活得最久。",
                    "配给站，老地方。该换成本事的东西，别搁着。",
                    "写规矩的人大概不喜欢你这种不听话、又不给他添乱的店主。挺好。",
                    "再做够下一阶的流水，我还来。"
                };
            }

            var root = StoreClientDialogList.MentorIntroDialog();
            if (root == null)
            {
                PerkShopLog.Warning("Native mentor dialogue template returned null.");
                return null;
            }

            Dialogue previous = null;
            var current = root;
            for (var i = 0; i < lines.Length; i++)
            {
                if (current == null)
                {
                    current = new Dialogue();
                    if (previous != null)
                    {
                        previous.nextDialogue = current;
                    }
                }

                current.SetText(speaker, lines[i]);
                current.SetEndAction((Il2CppSystem.Action)(System.Action)(() => { }));
                previous = current;
                current = current.nextDialogue;
            }

            if (previous == null)
            {
                PerkShopLog.Warning("Native mentor dialogue template had no usable nodes.");
                return null;
            }

            previous.nextDialogue = null;
            var playerLine = previous.NextDialogue();
            playerLine.SetText("PLAYER", "\u8c22\u8c22\u3002");
            playerLine.SetEndAction((Il2CppSystem.Action)(System.Action)(() => CompleteMentorVisit(store, intro, visitNumber)));

            return root;
        }

        private void CompleteMentorVisit(PlayerStore store, bool intro, int visitNumber)
        {
            try
            {
                PerkShopLog.Msg("Mentor visit completed. intro=" + intro + " visit=" + visitNumber);
                if (intro)
                {
                    GrantMentorNote(store);
                    _mentorIntroSeen = true;
                    _mentorVisitCount = Math.Max(_mentorVisitCount, visitNumber);
                    WriteInt(store, MentorIntroSeenKey, 1);
                    WriteInt(store, MentorVisitCountKey, _mentorVisitCount);

                    if (store.tutorialEnable && !_mentorNativeTutorialDone)
                    {
                        try
                        {
                            TutorialUIManager.Instance?.OnMentorDone();
                        }
                        catch (Exception ex)
                        {
                            PerkShopLog.Warning("Calling TutorialUIManager.OnMentorDone after mod mentor failed: " + ex.Message);
                        }
                    }

                    if (_pointsEarned > _mentorPromptedLevel)
                    {
                        ScheduleMentorOnNextDay(store);
                    }
                }
                else
                {
                    _mentorPromptedLevel = _pointsEarned;
                    _mentorNextVisitDay = -1;
                    _mentorVisitCount = Math.Max(_mentorVisitCount, visitNumber);
                    WriteInt(store, MentorPromptedLevelKey, _mentorPromptedLevel);
                    WriteInt(store, MentorNextVisitDayKey, _mentorNextVisitDay);
                    WriteInt(store, MentorVisitCountKey, _mentorVisitCount);
                }

                ShowNextPointNotification();
                _mentorVisitQueued = false;
                _savePending = true;
            }
            catch (Exception ex)
            {
                PerkShopLog.Error("CompleteMentorVisit failed: " + ex);
            }
            finally
            {
                try
                {
                    MelonCoroutines.Start(DismissMentorNextFrame(store));
                }
                catch (Exception ex)
                {
                    PerkShopLog.Error("Scheduling mentor dismissal failed: " + ex);
                }
            }
        }

        private IEnumerator DismissMentorNextFrame(PlayerStore store)
        {
            yield return new WaitForEndOfFrame();

            try
            {
                if (store == null || !CanUseInRun(store) || !IsPerkShopMentorCurrent(store))
                {
                    yield break;
                }

                store.DismissCurrentClient(false, true, true);
            }
            catch (Exception ex)
            {
                PerkShopLog.Error("Deferred mentor dismissal failed: " + ex);
            }
        }
        private void GrantMentorNote(PlayerStore store)
        {
            try
            {
                var note = IntelHelper.CreateMentorNoteBasicTrading();
                if (note == null)
                {
                    PerkShopLog.Warning("Mentor note creation returned null.");
                    return;
                }

                IntelHelper.InitTutorialPage(note, "导师笔记：体悟", BuildMentorNoteBody(), "——导师");
                store.AddDirectSellingItemToTable(note, true, false, false, 0);
            }
            catch (Exception ex)
            {
                PerkShopLog.Error("GrantMentorNote failed: " + ex);
            }
        }

        private string BuildMentorNoteBody()
        {
            var currentCost = GetPointCost(0);
            var nextCost = GetPointCost(Math.Max(1, _pointsEarned));
            var progress = GetProgressToNextPoint();
            return "你在买卖中磨出的眼力，会沉淀为“体悟”。\n\n"
                + "每当新增交易额达到 " + currentCost.ToString("N0", CultureInfo.InvariantCulture) + " 信用点，你就会获得 1 点天赋点。\n\n"
                + "前往配给站，点击“交易”上方的【体悟】，即可用天赋点购买天赋。\n\n"
                + "手中天赋点：" + _points + "\n\n"
                + "下一阶还需新增 " + nextCost.ToString("N0", CultureInfo.InvariantCulture) + " 信用点；当前已累积 " + progress.ToString("N0", CultureInfo.InvariantCulture) + "。";
        }

        private void ShowNextPointNotification()
        {
            try
            {
                var ui = StoreUIManager.Instance;
                if (ui == null)
                {
                    return;
                }

                var nextCost = GetPointCost(_pointsEarned);
                ui.Notify("下一级需要交易额：" + nextCost.ToString("N0", CultureInfo.InvariantCulture) + " 信用点", "default");
            }
            catch (Exception ex)
            {
                PerkShopLog.Error("ShowNextPointNotification failed: " + ex);
            }
        }

        private void ScheduleMentorForPoint(PlayerStore store)
        {
            if (!CanUseInRun(store) || _pointsEarned <= _mentorPromptedLevel || _mentorNextVisitDay > 0)
            {
                return;
            }

            ScheduleMentorOnNextDay(store);
        }

        private void ScheduleMentorOnNextDay(PlayerStore store)
        {
            var day = GetCurrentDaySafe();
            var targetDay = day <= 1 ? 2 : day + 1;
            if (_mentorNextVisitDay < 0 || targetDay < _mentorNextVisitDay)
            {
                _mentorNextVisitDay = targetDay;
                WriteInt(store, MentorNextVisitDayKey, _mentorNextVisitDay);
                _savePending = true;
                PerkShopLog.Msg("Scheduled perk mentor visit for day " + _mentorNextVisitDay + ".");
            }
        }

        private static int GetCurrentDaySafe()
        {
            try
            {
                return StoreStation.GetDayCounter();
            }
            catch
            {
                return 0;
            }
        }
    }

    [HarmonyPatch(typeof(TutorialUIManager), "OnMentorDone")]
    internal static class PerkShopTutorialMentorDonePatch
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            try { PerkShopMod.Instance?.OnNativeTutorialMentorDone(); }
            catch (Exception ex) { PerkShopLog.Error("TutorialUIManager.OnMentorDone 补丁失败：" + ex); }
        }
    }
}