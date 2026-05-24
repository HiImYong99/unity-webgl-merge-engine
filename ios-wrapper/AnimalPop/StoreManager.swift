import Foundation
import StoreKit

/// StoreKit 2 인앱 결제 관리.
/// 비소모성 상품 `remove_ads_hint_pack` (광고 제거 + 힌트 10개).
/// Android `BillingManager`와 동일 의미.
///
/// App Store Connect에 동일 product ID 등록 필요.
@available(iOS 15.0, *)
final class StoreManager {

    static let productId = "remove_ads_hint_pack"

    /// 콜백 → JS (productId, token)
    var onSuccess: ((String, String) -> Void)?
    var onFailed: ((String, Int) -> Void)?
    var onCancelled: ((String) -> Void)?
    var onRestored: ((String, String) -> Void)?

    private var updatesTask: Task<Void, Never>?
    /// 이미 처리한 트랜잭션 — purchase() 인라인 경로와 Transaction.updates 경로가
    /// 같은 트랜잭션을 중복 지급하지 않도록 dedupe (@MainActor 접근이라 락 불필요)
    private var handledTransactionIDs = Set<UInt64>()

    /// 앱 시작 시 호출 — 미완료/Ask-to-Buy/백그라운드 완료 트랜잭션 수신.
    /// updates 스트림은 "완료해야 할 트랜잭션"이므로 신규 구매로 처리(onSuccess).
    func startObserving() {
        updatesTask = Task { [weak self] in
            for await result in Transaction.updates {
                await self?.handle(verificationResult: result, isRestore: false)
            }
        }
    }

    func stopObserving() {
        updatesTask?.cancel()
    }

    // ── 결제 ─────────────────────────────────────────────────────
    func purchase(_ id: String) {
        Task {
            do {
                let products = try await Product.products(for: [id])
                guard let product = products.first else {
                    onFailed?(id, -1)
                    return
                }
                let result = try await product.purchase()
                switch result {
                case .success(let verification):
                    await handle(verificationResult: verification, isRestore: false)
                case .userCancelled:
                    onCancelled?(id)
                case .pending:
                    break // Ask-to-Buy: Transaction.updates에서 처리
                @unknown default:
                    onFailed?(id, -2)
                }
            } catch {
                NSLog("[StoreManager] 결제 실패: \(error.localizedDescription)")
                onFailed?(id, -3)
            }
        }
    }

    // ── 복원 ─────────────────────────────────────────────────────
    func restore() {
        Task {
            // AppStore.sync()는 사용자 Apple ID 재인증을 유발할 수 있어
            // 현재 entitlement 순회로 충분 (비소모성).
            for await result in Transaction.currentEntitlements {
                await handle(verificationResult: result, isRestore: true)
            }
        }
    }

    // ── 검증 + 콜백 ───────────────────────────────────────────────
    @MainActor
    private func handle(verificationResult result: VerificationResult<Transaction>, isRestore: Bool) async {
        guard case .verified(let transaction) = result else {
            NSLog("[StoreManager] 검증 실패 트랜잭션")
            return
        }
        // 환불/취소된 구매: 보상 재지급 방지 — 콜백 없이 종료 처리
        if transaction.revocationDate != nil {
            await transaction.finish()
            return
        }
        // 중복 지급 방지: 같은 트랜잭션이 두 경로로 와도 1회만 콜백
        guard handledTransactionIDs.insert(transaction.id).inserted else {
            await transaction.finish()
            return
        }
        let id = transaction.productID
        let token = String(transaction.id)
        if isRestore {
            onRestored?(id, token)
        } else {
            onSuccess?(id, token)
        }
        await transaction.finish()
    }
}
