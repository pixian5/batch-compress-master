import AppKit
import CoreGraphics
print("screen=" + String(describing: NSScreen.main?.frame ?? .zero))
print("display=" + String(describing: CGDisplayBounds(CGMainDisplayID())))
print("pixels=\(CGDisplayPixelsWide(CGMainDisplayID()))x\(CGDisplayPixelsHigh(CGMainDisplayID()))")
