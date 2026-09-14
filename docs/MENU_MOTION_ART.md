# Menu motion assets

Created using built-in Imagegen; original background-landscape.png is preserved.

Runtime assets under Assets/_Project/07_Art/UI/GlimblehopMenu:
- background-landscape-clean.png: corrected hands (one thumb and three fingers), both eyes open on the foreground and hat goblins, heel-facing wing on token 5, flag cloth removed with forest reconstructed.
- Resources/GlimblehopMenu/finish-flag.png: transparent cloth sprite; poles remain on the background.
- Resources/GlimblehopMenu/background-blink.png: aligned closed-eye frame. Both foreground eye regions are sampled at runtime, with feathered patch boundaries.

Final prompt set (built-in edit workflow):
1. Preserve the exact composition, camera, lighting, empty left menu area and character identities. Correct hands to four total digits; open both eyes only on the hat goblin pushing token 3. Preserve the foreground wink and far goblin's closed eyes.
2. Correct token 5 to an upright right-facing boot with ONE wing trailing left toward the heel. Remove ONLY flag cloth; reconstruct forest and preserve poles and ties. Keep all other elements aligned.
3. Extract ONLY red/cream checkerboard flag cloth on genuine transparent alpha, without poles, ropes, scenery or cast shadows. Preserve its perspective and handmade material.
4. Edit only the foreground goblin's open eye to a naturally closed green eyelid. Preserve all other details and image registration for eye-patch compositing.

Animation is implemented in MenuCharacterMotion and MenuAmbientView using UI Toolkit. Background-aligned parts use centered cover scaling for the 1672 x 941 source. The larger flag has fixed endpoints and a stronger wave; birds cross the distant upper clearing. No gameplay random state is used.

Inspector: MainMenuController > Atmosphere. Flag Enabled toggles movement (cloth stays visible); Blink Enabled, Second Goblin Blink Enabled and Blink Pause Range control independent random blinking (default 3–8 seconds). Birds Enabled and Bird Interval Seconds control the two distant silhouettes. Existing dust and leaf settings are preserved.

Validation: Unity MCP compilation and Play Mode checks; inspected the fully closed eye and a bird-flight frame, checked flag attachment and transparency, restored the animation scheduler after preview. No runtime errors were reported in the final Play Mode console check.

Latest built-in edit prompt: Open the foreground goblin’s viewer-right eye to match his other open eye. Repair the far goblin’s raised hands, especially his left hand, to one thumb and three fingers. Preserve pose, composition, lighting, token symbols and the background without flag cloth.

Latest validation: inspected both open and fully closed eye frames in Unity Play Mode against the updated background, including the enlarged flag and visible birds.
