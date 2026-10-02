package ir.soransoftpro.pastry;

import android.content.Context;
import android.content.SharedPreferences;
import android.os.Build;
import android.security.keystore.KeyGenParameterSpec;
import android.security.keystore.KeyPermanentlyInvalidatedException;
import android.security.keystore.KeyProperties;
import android.util.Base64;

import java.nio.charset.StandardCharsets;
import java.security.KeyStore;
import java.security.KeyStoreException;
import java.security.NoSuchAlgorithmException;
import java.security.UnrecoverableKeyException;
import java.security.cert.CertificateException;
import java.util.Arrays;

import javax.crypto.Cipher;
import javax.crypto.KeyGenerator;
import javax.crypto.SecretKey;
import javax.crypto.spec.GCMParameterSpec;

/** Stores only AES-GCM ciphertext; the non-exportable key requires per-use biometric auth. */
final class BiometricTokenStore {
    private static final String PREFS = "app_lock";
    private static final String ENABLED = "biometric_enabled";
    private static final String CIPHERTEXT = "biometric_token_ciphertext";
    private static final String EXPIRY = "biometric_token_expiry_ms";
    private static final String SESSION_MARKER = "sugarshop-biometric-session-v1";
    private static final String KEY_ALIAS = "sugarshop_biometric_token_v1";
    private static final String ANDROID_KEYSTORE = "AndroidKeyStore";
    private static final String TRANSFORMATION = "AES/GCM/NoPadding";

    private final SharedPreferences prefs;

    BiometricTokenStore(Context context) {
        prefs = context.getSharedPreferences(PREFS, Context.MODE_PRIVATE);
    }

    boolean isEnabled() {
        return prefs.getBoolean(ENABLED, false);
    }

    boolean hasUsableToken() {
        return !prefs.getString(CIPHERTEXT, "").isEmpty()
                && prefs.getLong(EXPIRY, 0L) > System.currentTimeMillis();
    }

    Cipher newEncryptionCipher() throws Exception {
        Cipher cipher = Cipher.getInstance(TRANSFORMATION);
        try {
            cipher.init(Cipher.ENCRYPT_MODE, getKey(true));
        } catch (KeyPermanentlyInvalidatedException e) {
            KeyStore keyStore = loadKeyStore();
            if (keyStore.containsAlias(KEY_ALIAS)) keyStore.deleteEntry(KEY_ALIAS);
            cipher = Cipher.getInstance(TRANSFORMATION);
            cipher.init(Cipher.ENCRYPT_MODE, getKey(true));
        }
        return cipher;
    }

    Cipher newDecryptionCipher() throws Exception {
        if (!hasUsableToken()) throw new IllegalStateException("Biometric token is missing or expired");
        byte[] combined = Base64.decode(prefs.getString(CIPHERTEXT, ""), Base64.NO_WRAP);
        if (combined.length <= 12) throw new IllegalArgumentException("Biometric token ciphertext is invalid");
        byte[] iv = Arrays.copyOfRange(combined, 0, 12);
        try {
            Cipher cipher = Cipher.getInstance(TRANSFORMATION);
            cipher.init(Cipher.DECRYPT_MODE, getKey(false), new GCMParameterSpec(128, iv));
            return cipher;
        } finally {
            Arrays.fill(iv, (byte) 0);
            Arrays.fill(combined, (byte) 0);
        }
    }

    String decrypt(Cipher authenticatedCipher) throws Exception {
        byte[] combined = Base64.decode(prefs.getString(CIPHERTEXT, ""), Base64.NO_WRAP);
        if (combined.length <= 12) throw new IllegalArgumentException("Biometric token ciphertext is invalid");
        byte[] plaintext = null;
        try {
            plaintext = authenticatedCipher.doFinal(combined, 12, combined.length - 12);
            return new String(plaintext, StandardCharsets.UTF_8);
        } finally {
            Arrays.fill(combined, (byte) 0);
            if (plaintext != null) Arrays.fill(plaintext, (byte) 0);
        }
    }

    static boolean isSessionMarker(String value) {
        return SESSION_MARKER.equals(value);
    }

    boolean encryptAndEnable(long expiresAtMillis, Cipher authenticatedCipher) throws Exception {
        byte[] plaintext = SESSION_MARKER.getBytes(StandardCharsets.UTF_8);
        byte[] encrypted = null;
        byte[] combined = null;
        try {
            encrypted = authenticatedCipher.doFinal(plaintext);
            byte[] iv = authenticatedCipher.getIV();
            if (iv == null || iv.length != 12) throw new IllegalStateException("Unexpected AES-GCM IV");
            combined = new byte[iv.length + encrypted.length];
            System.arraycopy(iv, 0, combined, 0, iv.length);
            System.arraycopy(encrypted, 0, combined, iv.length, encrypted.length);
            return prefs.edit()
                    .putString(CIPHERTEXT, Base64.encodeToString(combined, Base64.NO_WRAP))
                    .putLong(EXPIRY, expiresAtMillis)
                    .putBoolean(ENABLED, true)
                    .commit();
        } finally {
            Arrays.fill(plaintext, (byte) 0);
            if (encrypted != null) Arrays.fill(encrypted, (byte) 0);
            if (combined != null) Arrays.fill(combined, (byte) 0);
        }
    }

    boolean clearTokenKeepEnabled() {
        return prefs.edit().remove(CIPHERTEXT).remove(EXPIRY).commit();
    }

    boolean disable() throws Exception {
        boolean keyDeleted = true;
        try {
            KeyStore keyStore = loadKeyStore();
            if (keyStore.containsAlias(KEY_ALIAS)) keyStore.deleteEntry(KEY_ALIAS);
        } catch (Exception ignored) {
            keyDeleted = false;
        }
        boolean preferencesCleared = prefs.edit()
                .remove(CIPHERTEXT)
                .remove(EXPIRY)
                .putBoolean(ENABLED, false)
                .commit();
        return keyDeleted && preferencesCleared;
    }

    private SecretKey getKey(boolean create) throws Exception {
        KeyStore keyStore = loadKeyStore();
        if (keyStore.containsAlias(KEY_ALIAS)) {
            try {
                java.security.Key key = keyStore.getKey(KEY_ALIAS, null);
                if (key instanceof SecretKey) return (SecretKey) key;
                if (!create) throw new UnrecoverableKeyException("Biometric key is unavailable");
            } catch (UnrecoverableKeyException e) {
                if (!create) throw e;
            }
            keyStore.deleteEntry(KEY_ALIAS);
        }
        if (!create) throw new UnrecoverableKeyException("Biometric key is missing");

        KeyGenerator generator = KeyGenerator.getInstance(KeyProperties.KEY_ALGORITHM_AES, ANDROID_KEYSTORE);
        KeyGenParameterSpec.Builder spec = new KeyGenParameterSpec.Builder(KEY_ALIAS,
                KeyProperties.PURPOSE_ENCRYPT | KeyProperties.PURPOSE_DECRYPT)
                .setBlockModes(KeyProperties.BLOCK_MODE_GCM)
                .setEncryptionPaddings(KeyProperties.ENCRYPTION_PADDING_NONE)
                .setUserAuthenticationRequired(true);
        if (Build.VERSION.SDK_INT >= 30) {
            spec.setUserAuthenticationParameters(0, KeyProperties.AUTH_BIOMETRIC_STRONG);
        } else {
            spec.setUserAuthenticationValidityDurationSeconds(-1);
        }
        if (Build.VERSION.SDK_INT >= 24) spec.setInvalidatedByBiometricEnrollment(true);
        generator.init(spec.build());
        return generator.generateKey();
    }

    private KeyStore loadKeyStore() throws KeyStoreException, CertificateException, NoSuchAlgorithmException,
            java.io.IOException {
        KeyStore keyStore = KeyStore.getInstance(ANDROID_KEYSTORE);
        keyStore.load(null);
        return keyStore;
    }
}
