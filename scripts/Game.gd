extends Node

# Signals for global game state events
signal song_started
signal song_finished
signal score_updated(new_score)

# Constants
const DEFAULT_SONG_PATH = "res://TestSong.tres"

# References
@onready var scrolling_lyrics = %ScrollingLyrics
@onready var pitch_detector = %PitchDetector
@onready var mic_input = %MicInput

func _ready():
	_setup_audio()
	
func _input(event):
	if event.is_action_pressed("ui_select"):
		_start_song()

func _setup_audio():
	# Ensure the mic is capturing
	# Reverted manual restart; letting Autoplay handle it + AudioMenu
	if mic_input:
		if not mic_input.playing:
			mic_input.playing = true
		print("MicInput bus: ", mic_input.bus)

func _start_song():
	print("Starting Song...")
	AudioManager.PlayMusic(load(DEFAULT_SONG_PATH))
	song_started.emit()
