﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using System.Linq;

public class GlobalController : MonoBehaviour {

	public static GlobalController gc;
	public TitleText editorTitleText;
	public static TitleText titleText;
	static SignUI signUI;
	static BlackFadeUI blackoutUI;
	static DialogueUI dialogueUI;
	public static PlayerController pc;
	public static bool dialogueOpen;
	static bool dialogueOpenedThisFrame = false;
	public static bool pauseEnabled = true;
	static bool paused = false;
	public static bool dialogueClosedThisFrame = false;
	static NPC currentNPC;
	public static PlayerFollower playerFollower;
	public static Save save;
	static CloseableUI pauseUI;
	public static bool inAnimationCutscene;
	static bool inAbilityGetUI;
	public static Animator abilityUIAnimator;
	public static BarUI bossHealthUI;

	public static bool xboxController = false;
	public static bool playstationController = false;

	static DialogueLine toActivate = null;

	static RespawnManager rm;
	public static InventoryController inventory;

	static Queue<NPC> queuedNPCs = new Queue<NPC>();

	static int saveSlot = 1;
	static ParallaxOption parallaxOption;

	public GameObject talkPrompt;
	public GameObject newDialoguePrompt;

	public bool uiClosedThisFrame = false;
	public static int openUIs = 0;

	void Awake() {
		if (gc == null) {
			gc = this;
		} else {
			// if this one's a duplicate, destroy
			Destroy(this.gameObject);
			return;
		}
		DontDestroyOnLoad(this);
		titleText = editorTitleText;
		dialogueUI = GetComponentInChildren<DialogueUI>();
		signUI = GetComponentInChildren<SignUI>();
		pc = GetComponentInChildren<PlayerController>();
		rm = GetComponent<RespawnManager>();
		playerFollower = gc.GetComponentInChildren<PlayerFollower>();
		save = gc.GetComponent<Save>();
		blackoutUI = GetComponentInChildren<BlackFadeUI>();
		pauseUI = GetComponentInChildren<PauseUI>();
		abilityUIAnimator = gc.transform.Find("PixelCanvas").transform.Find("AbilityGetUI").GetComponent<Animator>();
		inventory = gc.GetComponentInChildren<InventoryController>();
		parallaxOption = gc.GetComponentInChildren<ParallaxOption>();
		bossHealthUI = GameObject.Find("BossHealthUI").GetComponent<BarUI>();
		bossHealthUI.gameObject.SetActive(false);
	}

	public static void ShowTitleText(string title, string subTitle = null) {
		titleText.ShowText(title, subTitle);
	}

	public static bool HasSavedGame() {
		return gc.GetComponent<BinarySaver>().HasSavedGame(saveSlot);
	}

	public static void NewGamePlus() {
		gc.GetComponent<BinarySaver>().NewGamePlus();
		Save s = gc.GetComponent<Save>();
		pc.LoadFromSaveData(s);
		LoadScene("Paradise/Tutorial");
	}

	public static bool HasBeatGame() {
		return gc.GetComponent<BinarySaver>().HasFinishedGame();
	}

	static void OpenInventory() {
		inventory.ShowInventory();
		pc.EnterCutscene(invincible:false);
	}

	static void CloseInventory() {
		inventory.HideInventory();
		pc.ExitCutscene();
	}

	void LateUpdate() {
		if (Input.GetKeyDown(KeyCode.R) && SceneManager.GetActiveScene().name.Equals("TargetTest")) {
			SceneManager.LoadScene(SceneManager.GetActiveScene().name);
		}

		if (inAbilityGetUI && InputManager.ButtonDown(Buttons.JUMP)) {
			HideAbilityGetUI();
		}

		bool inInventory = inventory.inventoryUI.animator.GetBool("Shown");
		if ((Input.GetButtonDown("Inventory") || (Input.GetButtonDown(Buttons.SPECIAL)) && inInventory)) {
			if (inInventory) {
				CloseInventory();
			} else if (!pc.inCutscene && pc.IsGrounded()) {
				OpenInventory();
			}
		} else if (inInventory) {
			// avoid any pre-late update weirdness
			pc.EnterCutscene();
		}

		
		if (Input.GetButtonDown("Start") && pauseEnabled && !inInventory && !paused) {
			// pauseUI takes care of unpausing
			Pause();
		}
		
		if (InputManager.GenericContinueInput()) {
			GlobalController.OnDialogueSkip();
		}

		dialogueOpenedThisFrame = false;
		dialogueClosedThisFrame = false;

		UpdateControllerStatus();

		if (Input.GetKeyDown(KeyCode.S)) {
			GlobalController.SaveGame(false);
		} else if (Input.GetKeyDown(KeyCode.L)) {
			GlobalController.LoadGame();
		}
	}

	public static void OnDialogueSkip() {
		if (!dialogueOpen || dialogueOpenedThisFrame || inAnimationCutscene) {
			return;
		}

		if (dialogueOpen && openUIs > 1) {
			return;
		}

		if (dialogueUI.slowRendering) {
			dialogueUI.CancelSlowRender();
			return;
		}

		if (dialogueUI.switchingImage) {
			dialogueUI.SwitchSpeakerImage();
		}

		//advance dialogue line or close
		//if necessary, hit the activatable from the previous line
		//and block dialogue/enter cutscene if necessary
		if (toActivate != null) {
			toActivate.activatable.Activate();
			if (toActivate.blocking) {
				//block dialogue line rendering and hide dialogue UI
				EnterCutscene();
				//don't render the dialogue
				toActivate = null;
				return;
			}
			toActivate = null;
		}

		DialogueLine nextLine = currentNPC.GetNextLine();

		if (nextLine != null) {
			dialogueUI.RenderDialogueLine(
				nextLine, 
				currentNPC.hasNextLine() || queuedNPCs.Count>0
			);
			if (nextLine.activatable != null) {
				if (!nextLine.activatesOnLineEnd) {
					nextLine.activatable.Activate();
				} else {
					toActivate = nextLine;
				}
			}
		} else {
			ExitDialogue();
		}
	}

	public static void EnterDialogue(NPC npc, bool fromQueue=false) {
		Hitstop.Interrupt();
		if (dialogueOpen) {
			queuedNPCs.Enqueue(npc);
			return;
		}
		pc.EndCombatStanceCooldown();
		if (!fromQueue) dialogueUI.Open();
		currentNPC = npc;
		dialogueOpenedThisFrame = true;
		dialogueUI.ShowNameAndPicture(npc.GetCurrentLine());
	}

	public static void ExitDialogue() {
		dialogueOpen = false;
		dialogueClosedThisFrame = true;
		if (currentNPC != null) {
			currentNPC.CloseDialogue();
		}
		currentNPC = null;
		if (queuedNPCs.Count != 0) {
			EnterDialogue(queuedNPCs.Dequeue(), fromQueue: true);
			FinishOpeningLetterboxes();
		} else {
			dialogueUI.Close();
		}
	}

	public static void FinishOpeningLetterboxes() {
		dialogueOpen = true;
		inAnimationCutscene = false;
		DialogueLine nextLine = currentNPC.GetNextLine();
		if (nextLine != null) {
			dialogueUI.RenderDialogueLine(nextLine, currentNPC.hasNextLine(), fromCutscene: true);
			if (nextLine.activatable != null) {
				if (!nextLine.activatesOnLineEnd) {
					nextLine.activatable.Activate();
				} else {
					toActivate = nextLine;
				}
			}
		} else {
			ExitDialogue();
		}
	}

	public static void FinishClosingLetterboxes() {

	}

	public static void OpenSign(string text, Vector2 position) {
		if (!signUI.IsVisible()) {
			signUI.SetText(text);
			signUI.SetPosition(position);
			signUI.Show();
		}
	}

	public static void CloseSign() {
		signUI.Hide();
	}

	public static Vector2 GetPlayerPos() {
		return pc.transform.position;
	}

	public static void Respawn() {
		rm.RespawnPlayer();
	}

	//called when the new respawn scene is loaded
	public static void StartPlayerRespawning() {
		pc.StartRespawning();
	}

	public static void AddGameFlag(GameFlag f) {
		if (!save.gameFlags.Contains(f)) {
			save.gameFlags.Add(f);
			PropagateFlagChange();
		}
	}

	public static void PropagateFlagChange() {
		foreach (SwitchOnStateImmediate i in FindObjectsOfType<SwitchOnStateImmediate>()) {
			i.ReactToStateChange();
		}
		foreach (StatefulNPC n in FindObjectsOfType<StatefulNPC>()) {
			n.ReactToStateChange();
		}
	}

	public static void RemoveGameFlag(GameFlag f) {
		if (save.gameFlags.Contains(f)) {
			save.gameFlags.Remove(f);
			PropagateFlagChange();
		}
	}

	public static bool HasFlag(GameFlag f) {
		if (save == null || f == GameFlag.None) {
			return false;
		}
		return save.gameFlags.Contains(f);
	}

	public static void PropagateStateChange(bool immediateOnly=true) {
		// all loaded objects, including inactive ones
		List<EnableOnGameState> immediates = (Resources.FindObjectsOfTypeAll(typeof(EnableOnGameState)) as EnableOnGameState[])
			.Where(x => immediateOnly ? x.immediate : true).ToList();
		foreach (EnableOnGameState i in immediates) {
			i.CheckState();
		}

		UpdateStatefulNPCs();

		Animator playerAnimator = pc.GetComponent<Animator>();
		playerAnimator.logWarnings = true;
		foreach (string s in save.gameStates) {
			if (s.StartsWith("anim_")) {
				playerAnimator.SetBool(s, true);
			}
		}
	}

	public static void AddState(GameState state) {
		if (state == null) return;
		save.gameStates.Add(state.stateName);
		PropagateStateChange();
	}

	public static void AddStates(List<GameState> states) {
		foreach (GameState state in states) {
			save.gameStates.Add(state.stateName);
		}
		PropagateStateChange();
	}

	public static bool HasState(GameState state) {
		return !save || save.gameStates.Contains(state.stateName);
	}

	public static void RemoveState(GameState state) {
		save.gameStates.Remove(state.stateName);
		PropagateStateChange();
	}

	public static void LoadScene(string sceneName, Beacon beacon=Beacon.None) {
		gc.GetComponent<TransitionManager>().LoadScene(sceneName, beacon);
	}

	public static void LoadSceneToPosition(string sceneName, Vector2 position) {
		gc.GetComponent<TransitionManager>().LoadSceneToPosition(sceneName, position);
	}

	public static void MovePlayerTo(Vector2 position, bool fade=false) {
		if (fade) {
			gc.StartCoroutine(gc.MovePlayerWithFade(position));
			return;
		}
		playerFollower.DisableSmoothing();
		pc.DisableTrails();
		pc.transform.position = position;
		pc.EnableTrails();
		playerFollower.SnapToPlayer();
		playerFollower.EnableSmoothing();
	}

	public IEnumerator MovePlayerWithFade(Vector2 position) {
		pc.EnterCutscene();
		FadeToBlack();
		yield return new WaitForSeconds(0.5f);
		playerFollower.DisableSmoothing();
		pc.DisableTrails();
		pc.transform.position = position;
		pc.EnableTrails();
		playerFollower.SnapToPlayer();
		playerFollower.EnableSmoothing();
		UnFadeToBlack();
		pc.ExitCutscene();
	}

	public static void MovePlayerToBeacon(Beacon beacon) {
		BeaconWrapper b = Object.FindObjectsOfType<BeaconWrapper>().Where(
			x => x.beacon == beacon
		).First();
		MovePlayerTo(b.transform.position);
	}

	public static void FadeToBlack() {
		blackoutUI.Show();
	}

	public static void UnFadeToBlack() {
		blackoutUI.Hide();
	}

	public static void FlashWhite() {
		blackoutUI.FlashWhite();
	}

	public static void ShowUI() {
		foreach (BarUI b in gc.GetComponentsInChildren<BarUI>(includeInactive:true)) {
			b.gameObject.SetActive(true);
		}
		bossHealthUI.gameObject.SetActive(false);
		inventory.moneyUI.gameObject.SetActive(true);
	}

	public static void HideUI() {
		foreach (BarUI b in gc.GetComponentsInChildren<BarUI>()) {
			b.gameObject.SetActive(false);
		}
		inventory.moneyUI.gameObject.SetActive(false);
	}

	public void ExitGame() {
		Application.Quit();
	}

	//called from a cutscene animation to finish it and resume dialogue
	public static void CutsceneCallback() {
		if (!dialogueOpen) return;
		// show the dialogue UI if there's a next line
		// catch NPC being hidden by an activated animation
		if (currentNPC != null && currentNPC.hasNextLine()) {
			dialogueUI.Open();
		} else {
			ExitDialogue();
		}
	}

	// hide dialogue UI but keep the player frozen
	// dialogue being open is a prerequisite for the cutscene state :^(
	public static void EnterCutscene() {
		inAnimationCutscene = true;
		if (dialogueOpen) {
			dialogueUI.Close();
		}
	}

	public static void EnterSlowMotion() {
		Time.timeScale = 0.3f;
	}

	public static void ExitSlowMotion() {
		Time.timeScale = 1;
	}

	public static void LoadGame() {
		FadeToBlack();
		gc.GetComponent<BinarySaver>().LoadGame();
		Save s = gc.GetComponent<Save>();
		pc.LoadFromSaveData(s);
		foreach (PersistentObject o in FindObjectsOfType<PersistentObject>()) {
			o.Start();
		}
		inventory.UpdateMoneyUI();
 	}

	public static void SaveGame(bool autosave=false) {
		if (save.unlocks.HasAbility(Ability.Heal) && !autosave) {
			AlerterText.Alert("Rebuilding waveform");
			pc.FullHeal();
			AlerterText.Alert("Done");
		}
		gc.GetComponent<BinarySaver>().SaveGame();
	}

	public static void Pause() {
		if (pc.inCutscene) {
			return;
		}
		paused = true;
		pauseUI.Open();
	}

	public static void Unpause() {
		paused = false;
	}

	public static SerializedPersistentObject GetPersistentObject(string id) {
		if (save == null) {
			return null;
		}
		return save.GetPersistentObject(id);
	}

	public static void SavePersistentObject(SerializedPersistentObject o) {
		if (save == null) {
			return;
		}
		save.SavePersistentObject(o);
	}

	static void UpdateControllerStatus() {
		string[] names = Input.GetJoystickNames();
		for (int x = 0; x < names.Length; x++)
		{
			if (names[x].Length == 19)
			{
				playstationController = true;
				xboxController = false;
			}
			if (names[x].Length == 33)
			{
				playstationController = false;
				xboxController = true;

			}
		}
	}

	public static void AddItem(Item item) {
		if (!item.IsAbility()) {
			if (item.count != 1)
				AlerterText.Alert($"{item.name} ({item.count}) acquired");
			else 
				AlerterText.Alert(item.name + " acquired");
		}
		if (item.gameStates != null) {
			AddStates(item.gameStates);
		}
		inventory.AddItem(item);
		PropagateItemChange();
	}

	public static void PropagateItemChange(bool immediateOnly=true) {
		List<EnableOnItem> immediates = (Resources.FindObjectsOfTypeAll(typeof(EnableOnItem)) as EnableOnItem[])
			.Where(x => immediateOnly ? x.immediate : true).ToList();
		foreach (EnableOnItem i in immediates) {
			i.CheckState();
		}
		UpdateStatefulNPCs();
	}

	static void UpdateStatefulNPCs() {
		foreach (StatefulNPC n in FindObjectsOfType<StatefulNPC>()) {
			n.ReactToStateChange();
		}
	}

	public static void ShowAbilityGetUI() {
		abilityUIAnimator.SetTrigger("Show");
		pc.EnterCutscene();
		// to keep the player from accidentally skipping the animation early
		gc.Invoke("EnterAbilityUI", 1f);
	}

	void EnterAbilityUI() {
		inAbilityGetUI = true;
	}

	public static void HideAbilityGetUI() {
		pc.ExitCutscene();
		SoundManager.InteractSound();
		abilityUIAnimator.SetTrigger("Hide");
		inAbilityGetUI = false;
	}

	public static void UnlockAbility(Ability a) {
		save.UnlockAbility(a);
	}

	static NPC MakeItemPickupDialogue(Item item) { 
		NPCConversations conversations = new NPCConversations();
		DialogueLine line = new DialogueLine();

		line.lineText = "You got the <color=aqua>" + item.name + "</color>.";
		line.speakerImage = item.detailedIcon;
		line.speakerName = "";

		//this was never meant to happen
		conversations.conversations = new List<Conversation>();
		conversations.conversations.Add(new Conversation(line));
		return new NPC(conversations);
	}

	public static void EnterMerchantDialogue(Merchant merchant) {
		pc.EnterCutscene();
		inventory.currentMerchant = merchant;
		OpenInventory();
	}

	public static void BoostStat(StatType statType, int amount) {
		switch (statType) {
			case StatType.HEALTH:
				int missing = pc.maxHP-pc.currentHP;
				pc.maxHP += amount;
				pc.currentHP = pc.maxHP-missing;
				break;
			case StatType.ENERGY:
				pc.maxEnergy += amount;
				break;
			case StatType.DAMAGE:
				pc.baseDamage += amount;
				break;
		}
		StatBoostUI.ReactToBoost(statType, amount);
	}

	public static void EnableParallax() {
		parallaxOption.moveParallax = true;
	}

	public static void DisableParallax() {
		parallaxOption.moveParallax = false;
	}

	public static void HidePlayer() {
		pc.EnterCutscene();
		pc.Hide();
	}

	public static void ShowPlayer() {
		pc.ExitCutscene();
		pc.Show();
	}
}
