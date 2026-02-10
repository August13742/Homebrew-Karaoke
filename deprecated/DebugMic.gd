extends Node

@export var mic_player: AudioStreamPlayer

func _process(delta):
	if mic_player:
		if mic_player.playing:
			pass
			# print("Mic Playing: ", mic_player.get_playback_position())
		else:
			print("Mic STOPPED!")
