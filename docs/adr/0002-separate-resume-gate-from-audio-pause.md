# Separate resume gating from audio pause

After an Esc, focus, platform, or advertisement pause ends, the session remains in a resume-wait state until an explicit pointer click, keeping gameplay and active time stopped. Audio pause follows only the active pause cause and may end before that click; tutorial and result presentation can likewise stop gameplay without muting calm or terminal music. Entering a terminal result clears any pending resume wait and keeps the pointer free. This keeps pointer-lock safety separate from audio lifecycle while pause causes remain composable.
