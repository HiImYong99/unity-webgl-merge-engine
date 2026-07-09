import Foundation
import UIKit
import GameKit

/// Game Center: 인증 / 점수 제출 / 리더보드 UI.
/// 웹의 🏆 리더보드 버튼 → `showLeaderboard`, 게임오버 점수 → `submit`.
/// App Store 4.2(최소 기능) 대응을 위한 네이티브 가치 요소.
///
/// App Store Connect에 리더보드 생성 후 ID 일치 필요.
final class GameCenterManager: NSObject {

    // ── [USER ACTION] App Store Connect 리더보드 ID와 일치 ──────────
    static let leaderboardId = "animalpop_highscore"
    // ───────────────────────────────────────────────────────────────

    private(set) var isAuthenticated = false
    /// 인증 시 표시해야 할 VC가 있으면 호출 (rootVC가 present)
    var presentAuthVC: ((UIViewController) -> Void)?
    /// 리더보드 요청이 미인증으로 확정 실패했을 때 (JS 안내 토스트용)
    var onUnavailable: (() -> Void)?
    /// 미인증 상태에서 리더보드 버튼을 누른 경우, 재인증 성공 후 바로 표시하기 위한 보류 VC
    private weak var pendingPresentVC: UIViewController?

    func authenticate() {
        GKLocalPlayer.local.authenticateHandler = { [weak self] vc, error in
            guard let self = self else { return }
            if let vc = vc {
                self.presentAuthVC?(vc)
                return
            }
            if let error = error {
                NSLog("[GameCenter] 인증 실패: \(error.localizedDescription)")
                self.isAuthenticated = false
                if self.pendingPresentVC != nil {
                    self.pendingPresentVC = nil
                    self.onUnavailable?()
                }
                return
            }
            self.isAuthenticated = GKLocalPlayer.local.isAuthenticated
            if let host = self.pendingPresentVC {
                self.pendingPresentVC = nil
                if self.isAuthenticated { self.present(from: host) }
                else { self.onUnavailable?() }
            }
        }
    }

    func submit(score: Int) {
        guard isAuthenticated else { return }
        GKLeaderboard.submitScore(score,
                                  context: 0,
                                  player: GKLocalPlayer.local,
                                  leaderboardIDs: [GameCenterManager.leaderboardId]) { error in
            if let error = error {
                NSLog("[GameCenter] 점수 제출 실패: \(error.localizedDescription)")
            }
        }
    }

    func presentLeaderboard(from vc: UIViewController) {
        guard isAuthenticated else {
            // 미인증: 재인증 시도 후 성공하면 바로 표시, 확정 실패면 onUnavailable (버튼 무반응 방지)
            pendingPresentVC = vc
            authenticate()
            return
        }
        present(from: vc)
    }

    private func present(from vc: UIViewController) {
        let gcVC = GKGameCenterViewController(leaderboardID: GameCenterManager.leaderboardId,
                                              playerScope: .global,
                                              timeScope: .allTime)
        gcVC.gameCenterDelegate = self
        vc.present(gcVC, animated: true)
    }
}

extension GameCenterManager: GKGameCenterControllerDelegate {
    func gameCenterViewControllerDidFinish(_ gameCenterViewController: GKGameCenterViewController) {
        gameCenterViewController.dismiss(animated: true)
    }
}
