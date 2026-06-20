
using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDK3.Components;
using VRC.SDKBase;
using VRC.Udon.Common;


namespace CameraSystem {
	[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
	public class CameraSystem_Console : UdonSharpBehaviour {
		[Header("Cameras")]
		public Camera[] camerasObjects;
		public RenderTexture[] camerasRenderTextures;
		public Material[] camerasMaterials;

		[Header("Controls and UI")]
		public Image[] sendLiveButtons;
		public TextMeshProUGUI currentCameraText;
		public TextMeshProUGUI[] deskCamerasFovTexts;
		public TextMeshProUGUI[] handheldsFovTexts;
		public TextMeshProUGUI[] followUsernameTexts;
		public VRCPickup[] handheldsVrcPickups;
		public GameObject initErrorWarningText;
		public Slider[] cameraFovSliders;
		public Button[] potatoButtons;
		// TODO prev btns, next btns, current username, selected username

		[Header("Feedbacks")]
		public Material liveMaterial;
		public Material cameraJack;

		[UdonSynced] private int currentCamera = 5;
		private int lastCamera = 5;
		[HideInInspector] [UdonSynced] public string[] cameraFollowUsername = new string[6];
		[HideInInspector] [UdonSynced] public bool[] cameraFollow = new bool[6];

		[SerializeField] private ViewTabletSpawner _ViewTabletSpawner;

		[Header("State colors")]
		public Color32 colorGreen = new Color(15/255f, 132/255f, 12/255f, 255/255f);
		public Color32 colorGrey = new Color(255f, 255f, 255f, 255/255f);
		public Color32 colorRed = new Color(255f, 0/255f, 0/255f, 255/255f);
		public Color32 colorBlack = new Color(0f, 0f, 0f, 255/255f);

		private bool isAuthorized = false;
		private bool isViewable = false;
		private bool potato = true;

		private bool PlayerHasLookedDown = false;
		private bool PlayerHasLookedUp = false;
		private float PlayerLookTimer = 0.0f;


		void Update()
		{
			if (!Networking.LocalPlayer.IsUserInVR() && Input.GetKeyDown(KeyCode.Tab))
			{
				ToggleViewable();
			}

			if (PlayerLookTimer > 0.0f)
			{
				PlayerLookTimer -= Time.deltaTime;
				if (PlayerLookTimer <= 0.0f)
				{
					PlayerHasLookedDown = PlayerHasLookedUp = false;
				}
			}
		}

		public override void InputLookVertical(float Value, UdonInputEventArgs Args)
		{
			if (Networking.LocalPlayer.IsUserInVR())
			{
				if (!PlayerHasLookedDown && Value < -0.85f)
				{
					PlayerHasLookedUp = false;
					PlayerHasLookedDown = true;
					PlayerLookTimer = 0.2f;
				}
				else if (!PlayerHasLookedUp && Value > 0.85f)
				{
					PlayerHasLookedUp = true;
				}

				if (PlayerHasLookedDown && PlayerHasLookedUp)
				{
					PlayerHasLookedDown = PlayerHasLookedUp = false;
					ToggleViewable();
				}
			}

			base.InputLookVertical(Value, Args);
		}


		public void Authorize() {
			Debug.Log($"[OTT_CAMERA_SYSTEM][authorize] User is now authorized to use the console");
			isAuthorized = true;

			SendLiveCamera(currentCamera);

			foreach (Button potatoButton in potatoButtons)
			{
				potatoButton.enabled = true;
			}

			foreach (VRCPickup vrcp in handheldsVrcPickups) {
				if (Utilities.IsValid(vrcp)) {
					vrcp.pickupable = false;
				}
			}
		}

		public void Deauthorize() {
			Debug.Log($"[OTT_CAMERA_SYSTEM][deauthorize] User is now forbidden to have fun");
			isAuthorized = false;
			iAmAPotato();

			foreach (Button potatoButton in potatoButtons)
			{
				potatoButton.enabled = false;
			}

			foreach (VRCPickup vrcp in handheldsVrcPickups) {
				if (Utilities.IsValid(vrcp)) {
					vrcp.pickupable = false;
				}
			}
		}

		public void ToggleViewable()
		{
			if (isViewable)
			{
				SetUnviewable();
			}
			else
			{
				SetViewable();
			}
		}

		public void SetViewable()
		{
			_EnableViewableLiveCamera_Private(currentCamera);

			isViewable = true;
			_ViewTabletSpawner.SpawnAtLocalPlayerHead();
		}

		public void SetUnviewable()
		{
			isViewable = false;
			foreach (Camera Cam in camerasObjects)
			{
				Cam.enabled = false;
			}
			_ViewTabletSpawner.Despawn();
		}

		void Start() {
			iAmAPotato();

			//Debug.Log($"[OTT_CAMERA_SYSTEM][Start] FOV of camera 1 is " + cameraFOV[0]);

			if (!sanityCheck()) {
				Debug.Log($"[OTT_CAMERA_SYSTEM][Start] Sanity check failed");
				return; // everything will be broken anyway
			}

			foreach (Image img in sendLiveButtons) {
				img.color = colorGrey;
			}

			// Set the current camera as live
			SendLiveCamera(currentCamera);

			// and disable pickupables
			foreach (VRCPickup vrcp in handheldsVrcPickups) {
				if (Utilities.IsValid(vrcp)) {
					vrcp.pickupable = false;
				}
			}
		}

		// Quick check for validity of all our basic needed objects
		private bool sanityCheck() {
			// TODO check everything + arrays > 0
			bool error = false;
			foreach (Camera cam in camerasObjects) {
				if (!Utilities.IsValid(cam)) {
					Debug.Log($"[OTT_CAMERA_SYSTEM][sanityCheck] Got an invalid item in the camerasObjects list");
					error = true;
				}
			}
			foreach (RenderTexture rt in camerasRenderTextures) {
				if (!Utilities.IsValid(rt)) {
					Debug.Log($"[OTT_CAMERA_SYSTEM][sanityCheck] Got an invalid item in the camerasRenderTextures list");
					error = true;
				}
			}
			foreach (Material mat in camerasMaterials) {
				if (!Utilities.IsValid(mat)) {
					Debug.Log($"[OTT_CAMERA_SYSTEM][sanityCheck] Got an invalid item in the camerasMaterials list");
					error = true;
				}
			}
			foreach (Image img in sendLiveButtons) {
				if (!Utilities.IsValid(img)) {
					Debug.Log($"[OTT_CAMERA_SYSTEM][sanityCheck] Got an invalid item in the sendLiveButtons list");
					error = true;
				}
			}
			foreach (Slider sl in cameraFovSliders) {
				if (!Utilities.IsValid(sl)) {
					Debug.Log($"[OTT_CAMERA_SYSTEM][sanityCheck] Got an invalid item in the cameraFovSliders list");
					error = true;
				}
			}
			if (!Utilities.IsValid(liveMaterial)) {
				Debug.Log($"[OTT_CAMERA_SYSTEM][sanityCheck] Invalid liveMaterial");
				error = true;
			}
			if (!Utilities.IsValid(cameraJack)) {
				Debug.Log($"[OTT_CAMERA_SYSTEM][sanityCheck] Invalid cameraJack");
				error = true;
			}
			if (!Utilities.IsValid(currentCameraText)) {
				Debug.Log($"[OTT_CAMERA_SYSTEM][sanityCheck] Invalid currentCameraText");
				error = true;
			}

			if (error && Utilities.IsValid(initErrorWarningText)) {
				initErrorWarningText.SetActive(true);
			}
			return !error;
		}

		public bool TogglePotato() {
			potato = !potato;
			if (potato) {
				iAmAPotato();
			} else {
				noLongerAPotato();
			}
			return potato;
		}

		private void noLongerAPotato() {
			Debug.Log($"[OTT_CAMERA_SYSTEM][noLongerAPotato]");
			
			for (int i = 0; i < camerasObjects.Length; i++) {
				camerasObjects[i].enabled = true;
			}

			foreach (Button potatoButton in potatoButtons)
			{
				potatoButton.GetComponent<Image>().color = colorGrey;
			}
		}

		private void iAmAPotato() {
			Debug.Log($"[OTT_CAMERA_SYSTEM][iAmAPotato]");
			
			// For each Camera, UNLESS current camera, disable it
			for (int i = 0; i < camerasObjects.Length; i++) {
				camerasObjects[i].enabled = currentCamera == i ? true : false;
			}

			foreach (Button potatoButton in potatoButtons)
			{
				potatoButton.GetComponent<Image>().color = colorGreen;
			}
		}

		public void SendLiveCamera(int index) {
			if (isAuthorized)
			{
				Networking.SetOwner(Networking.LocalPlayer, gameObject);

				// Set the previous button to grey
				sendLiveButtons[lastCamera].color = colorGrey;
				// Set the new button to red
				sendLiveButtons[index].color = colorRed;
				// Set the live material to the right render textures
				liveMaterial.SetTexture("_EmissionMap", camerasRenderTextures[index]);
				cameraJack.SetTexture("_MainTex", camerasRenderTextures[index]);
				// Set the text for the current camera name
				currentCameraText.text = $"Camera {index + 1}";

				// Finally set the current camera index
				currentCamera = index;
				lastCamera = index;

				// Update potato cameras if needed
				if (potato)
				{
					iAmAPotato();
				}
				else
				{
					noLongerAPotato();
				}

				RequestSerialization();
			}
			else if (isViewable)
			{
				currentCamera = index;
				lastCamera = index;
				_EnableViewableLiveCamera_Private(index);
			}
		}
		private void _EnableViewableLiveCamera_Private(int Index)
		{
			for (int i = 0; i < camerasObjects.Length; i++)
			{
				camerasObjects[i].enabled = (i == Index) ? true : false;
			}
			liveMaterial.SetTexture("_EmissionMap", camerasRenderTextures[Index]);
			cameraJack.SetTexture("_MainTex", camerasRenderTextures[Index]);
		}

		public void SendLiveCamera1() {
			if (isAuthorized) {
				SendLiveCamera(0);
			} else {
				Debug.Log($"[OTT_CAMERA_SYSTEM][sendLiveCamera1] Unauthorized action.");
			}
		}

		public void SendLiveCamera2() {
			if (isAuthorized) {
				SendLiveCamera(1);
			} else {
				Debug.Log($"[OTT_CAMERA_SYSTEM][sendLiveCamera2] Unauthorized action.");
			}
		}

		public void SendLiveCamera3() {
			if (isAuthorized) {
				SendLiveCamera(2);
			} else {
				Debug.Log($"[OTT_CAMERA_SYSTEM][sendLiveCamera3] Unauthorized action.");
			}
		}

		public void SendLiveCamera4() {
			if (isAuthorized) {
				SendLiveCamera(3);
			} else {
				Debug.Log($"[OTT_CAMERA_SYSTEM][sendLiveCamera4] Unauthorized action.");
			}
		}

		public void SendLiveCamera5() {
			if (isAuthorized) {
				SendLiveCamera(4);
			} else {
				Debug.Log($"[OTT_CAMERA_SYSTEM][sendLiveCamera5] Unauthorized action.");
			}
		}

		public void SendLiveCamera6() {
			if (isAuthorized) {
				SendLiveCamera(5);
			} else {
				Debug.Log($"[OTT_CAMERA_SYSTEM][sendLiveCamera6] Unauthorized action.");
			}
		}
		

		public override void OnDeserialization() {
			SendLiveCamera(currentCamera);
		}

		public override void OnOwnershipTransferred(VRCPlayerApi Player)
		{
			foreach (Camera CameraObject in camerasObjects)
			{
				Networking.SetOwner(Player, CameraObject.gameObject);
			}
			base.OnOwnershipTransferred(Player);
		}
	}
}
