using UnityEngine;

public class TouchInputHandler : MonoBehaviour
{
		public Vector2Int? InputState = null;
		
		bool isSwipe;
		float fingerStartTime;
		Vector2 fingerStartPos;
		public float minSwipeDist = 30.0f;
		public float maxSwipeTime = 0.5f;

		public void OnUpdate() {
			
			if (Input.touchCount > 0) {
				foreach (Touch touch in Input.touches) {
					Debug.Log("Touch: " + touch.phase);

					switch (touch.phase) {
						case TouchPhase.Began:
							/* this is a new touch */
							isSwipe = true;
							fingerStartTime = Time.time;
							fingerStartPos = touch.position;
							break;

						case TouchPhase.Canceled:
							/* The touch is being canceled */
							isSwipe = false;
							break;

						case TouchPhase.Ended:

							float gestureTime = Time.time - fingerStartTime;
							float gestureDist = (touch.position - fingerStartPos).magnitude;

							Debug.Log("Touch: time" + gestureTime + " dist: " + gestureDist);
							
							if (isSwipe && gestureTime < maxSwipeTime && gestureDist > minSwipeDist) {
								Vector2 direction = touch.position - fingerStartPos;
								Vector2 swipeType = Vector2.zero;

								if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y)) {
									// the swipe is horizontal:
									swipeType = Vector2.right*Mathf.Sign(direction.x);
								} else {
									// the swipe is vertical:
									swipeType = Vector2.up*Mathf.Sign(direction.y);
								}

								if (swipeType.x != 0.0f) {
									if (swipeType.x > 0.0f) {
										InputState = Vector2Int.right;
									} else {
										InputState = Vector2Int.left;
									}
								}

								if (swipeType.y != 0.0f) {
									if (swipeType.y > 0.0f) {
										InputState = Vector2Int.up;
									} else {
										InputState = Vector2Int.down;
									}
								}
							}

							break;
					}
				}
			}
		}
}
