/*****************************************************************************
 * SOURCE: https://github.com/videolan/vlc/blob/master/modules/gui/qt/player/qml/PIPPlayer.qml
 * PROJECT: VLC media player (VideoLAN)
 * LICENSE: GPL-2.0-or-later
 * 
 * KEY ARCHITECTURAL INSIGHTS:
 * 1. SINGLE decode pipeline — PIP uses the SAME VideoSurfaceProvider as main window (no second decoder)
 * 2. Controls auto-hide on non-hover (contentItem.visible depends on hoverHandler)
 * 3. Drag-to-move via Qt Quick DragHandler
 * 4. Double-click exits PIP (returns to main window)
 * 5. Drop shadow on video surface for depth
 * 6. Compositor factory pattern picks platform backend (DComp/Win7/X11/Wayland)
 *****************************************************************************/
import QtQuick
import QtQuick.Templates as T
import VLC.MainInterface
import VLC.Style
import VLC.Widgets as Widgets
import VLC.Playlist
import VLC.Player
import VLC.Util

T.Control {
    id: root
    
    width: Math.round(VLCStyle.dp(320, VLCStyle.scale))
    height: Math.round(VLCStyle.dp(180, VLCStyle.scale))
    objectName: "pip window"

    property real dragXMin: 0
    property real dragXMax: 0
    property real dragYMin: undefined
    property real dragYMax: undefined
    property int textStyle: Text.Outline

    Accessible.role: Accessible.Graphic
    Accessible.focusable: false
    Accessible.name: qsTr("video content")

    Drag.active: dragHandler.active
    Drag.onActiveChanged: {
        root.anchors.left = undefined
        root.anchors.right = undefined
        root.anchors.top = undefined
        root.anchors.bottom = undefined
        root.anchors.verticalCenter = undefined
        root.anchors.horizontalCenter = undefined
    }

    DoubleClickIgnoringItem {
        anchors.fill: parent

        TapHandler {
            gesturePolicy: TapHandler.WithinBounds
            onDoubleTapped: MainCtx.playerView = true
            onTapped: MainPlaylistController.togglePlayPause()
        }

        DragHandler {
            id: dragHandler
            target: root
            cursorShape: Qt.DragMoveCursor
            dragThreshold: 0
            grabPermissions: PointerHandler.CanTakeOverFromAnything
            xAxis.minimum: root.dragXMin
            xAxis.maximum: root.dragXMax
            yAxis.minimum: root.dragYMin
            yAxis.maximum: root.dragYMax
        }

        HoverHandler {
            id: hoverHandler
            grabPermissions: PointerHandler.CanTakeOverFromAnything
            cursorShape: Qt.ArrowCursor
            Component.onCompleted: {
                if (typeof blocking === 'boolean')
                    blocking = true // Qt 6.3 feature
            }
        }
    }

    background: VideoSurface {
        id: videoSurface
        videoSurfaceProvider: MainCtx.videoSurfaceProvider

        color: (hoverHandler.hovered ||
                playButton?.hovered ||
                closeButton?.hovered ||
                fullscreenButton?.hovered) ? "#10000000" : "transparent"

        Widgets.DefaultShadow {
        }
    }

    contentItem: Item {
        visible: hoverHandler.hovered ||
                 playButton.hovered ||
                 closeButton.hovered ||
                 fullscreenButton.hovered
        z: 1

        Widgets.IconButton {
            id: playButton
            anchors.centerIn: parent
            font.pixelSize: VLCStyle.icon_large
            description: qsTr("play/pause")
            text: (Player.playingState !== Player.PLAYING_STATE_PAUSED
                   && Player.playingState !== Player.PLAYING_STATE_STOPPED)
                  ? VLCIcons.pause_filled
                  : VLCIcons.play_filled
            textStyle: root.textStyle
            onClicked: MainPlaylistController.togglePlayPause()
        }

        Widgets.IconButton {
            id: closeButton
            anchors {
                top: parent.top
                topMargin: VLCStyle.margin_small
                right: parent.right
                rightMargin: VLCStyle.margin_small
            }
            font.pixelSize: VLCStyle.icon_PIP
            description: qsTr("close video")
            text: VLCIcons.close
            textStyle: root.textStyle
            onClicked: MainPlaylistController.stop()
        }

        Widgets.IconButton {
            id: fullscreenButton
            anchors {
                top: parent.top
                topMargin: VLCStyle.margin_small
                left: parent.left
                leftMargin: VLCStyle.margin_small
            }
            font.pixelSize: VLCStyle.icon_PIP
            description: qsTr("maximize player")
            text: VLCIcons.fullscreen
            textStyle: root.textStyle
            onClicked: MainCtx.playerView = true
        }
    }
}
