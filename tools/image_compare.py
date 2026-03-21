import sys

import numpy as np
from PIL import Image
from skimage.metrics import structural_similarity as ssim

def load_image(path):
    img = Image.open(path).convert("RGB")
    return np.array(img)

def compare(img1_path, img2_path, *, print_results=True):
    img1 = load_image(img1_path)
    img2 = load_image(img2_path)

    if img1.shape != img2.shape:
        raise ValueError("Image shapes do not match")

    # Mean absolute difference
    mad = np.mean(np.abs(img1.astype(np.float32) - img2.astype(np.float32)))

    # SSIM (convert to grayscale internally)
    score, diff = ssim(img1, img2, channel_axis=2, full=True)

    if print_results:
        print(f"SSIM: {score:.6f}")
        print(f"Mean Abs Diff: {mad:.4f}")

    return score, mad, diff


def compare_metrics(img1_path, img2_path):
    score, mad, _ = compare(img1_path, img2_path, print_results=False)
    return score, mad

if __name__ == "__main__":
    if len(sys.argv) != 3:
        print("Usage: python image_compare.py img1.png img2.png")
        sys.exit(1)

    compare(sys.argv[1], sys.argv[2], print_results=True)
