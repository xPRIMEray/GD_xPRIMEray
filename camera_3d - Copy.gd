extends Camera3D

@export var beta: float = 0.0
@export var gamma: float = 2.0

func get_curved_ray(ndc: Vector2) -> Vector3:
	var ray := project_ray_normal(ndc)
	var r := ray.length()
	var k := pow(r, gamma) * beta
	return (ray + ray.normalized() * k).normalized()

func _process(delta: float) -> void:
	# example debug test ray on center pixel:
	var warped = get_curved_ray(Vector2(0.0, 0.0))
	# print(warped)  # uncomment to see warp per frame
