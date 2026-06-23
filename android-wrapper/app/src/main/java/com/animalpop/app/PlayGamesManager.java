package com.animalpop.app;

import android.app.Activity;
import android.util.Log;

import com.google.android.gms.games.AuthenticationResult;
import com.google.android.gms.games.GamesSignInClient;
import com.google.android.gms.games.PlayGames;
import com.google.android.gms.games.PlayGamesSdk;

/**
 * Google Play Games Services v2 – 리더보드(전체 기간 최고점수) 연동.
 * iOS Game Center(GameCenterManager.swift)와 동일 역할: 자동 사인인 → 점수 제출 / 리더보드 UI 표시.
 *
 * ★ 배포 전 필수 교체 (Play Console > Play Games Services > 설정 및 관리 > 구성):
 *   res/values/strings.xml 의
 *     - game_services_project_id : PGS 프로젝트 ID(숫자)
 *     - leaderboard_highscore    : 생성한 리더보드 ID
 *   를 콘솔에서 발급받은 실제 값으로 교체. 그 전까지는 사인인이 실패하여 리더보드 버튼이 숨겨진 상태로 유지됨(죽은 버튼 없음).
 */
public class PlayGamesManager {

    private static final String TAG = "AnimalPop";
    static final int RC_LEADERBOARD = 9001;

    public interface ReadyCallback { void onReady(boolean authenticated); }

    private final Activity activity;
    private final ReadyCallback readyCallback;
    private final String leaderboardId;
    private boolean authenticated = false;

    PlayGamesManager(Activity activity, ReadyCallback readyCallback) {
        this.activity = activity;
        this.readyCallback = readyCallback;
        this.leaderboardId = activity.getString(R.string.leaderboard_highscore);
    }

    /** SDK 초기화 + 자동 사인인 상태 확인 (onCreate에서 메인 스레드로 호출) */
    void init() {
        PlayGamesSdk.initialize(activity);
        GamesSignInClient client = PlayGames.getGamesSignInClient(activity);
        client.isAuthenticated().addOnCompleteListener(task -> {
            authenticated = task.isSuccessful()
                    && task.getResult() != null
                    && task.getResult().isAuthenticated();
            Log.d(TAG, "[PGS] authenticated=" + authenticated);
            if (readyCallback != null) readyCallback.onReady(authenticated);
        });
    }

    boolean isAuthenticated() { return authenticated; }

    /** 게임오버 점수 제출 (미인증 시 무시) */
    void submitScore(long score) {
        if (!authenticated) { Log.d(TAG, "[PGS] submitScore skipped (not authenticated)"); return; }
        PlayGames.getLeaderboardsClient(activity).submitScore(leaderboardId, score);
        Log.d(TAG, "[PGS] submitScore " + score);
    }

    /** 리더보드 UI 표시 (미인증 시 사인인 1회 재시도 후 표시) */
    void showLeaderboard() {
        if (authenticated) { presentLeaderboardUi(); return; }
        Log.d(TAG, "[PGS] showLeaderboard: not authenticated, prompting sign-in");
        PlayGames.getGamesSignInClient(activity).signIn().addOnCompleteListener(task -> {
            authenticated = task.isSuccessful()
                    && task.getResult() != null
                    && task.getResult().isAuthenticated();
            if (readyCallback != null) readyCallback.onReady(authenticated);
            if (authenticated) presentLeaderboardUi();
        });
    }

    private void presentLeaderboardUi() {
        PlayGames.getLeaderboardsClient(activity)
                .getLeaderboardIntent(leaderboardId)
                .addOnSuccessListener(intent -> activity.startActivityForResult(intent, RC_LEADERBOARD))
                .addOnFailureListener(e -> Log.w(TAG, "[PGS] getLeaderboardIntent failed: " + e.getMessage()));
    }
}
