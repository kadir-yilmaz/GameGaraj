/**
 * Client-side credit card validator (Luhn, Expiry, CVV, Holder Name).
 * Mirrors server-side DataAnnotations in CheckoutInfoInput.
 */
window.CardValidator = (() => {
    function passesLuhn(digits) {
        let sum = 0;
        let alternate = false;

        for (let i = digits.length - 1; i >= 0; i--) {
            let n = parseInt(digits[i], 10);
            if (alternate) {
                n *= 2;
                if (n > 9) n -= 9;
            }
            sum += n;
            alternate = !alternate;
        }
        return sum % 10 === 0;
    }

    function validateCardHolder(value) {
        const trimmed = (value || '').trim();
        if (trimmed.length < 3) return 'Kart üzerindeki isim en az 3 karakter olmalıdır.';
        return '';
    }

    function validateCardNumber(value) {
        const digits = (value || '').replace(/\D/g, '');
        if (!digits) return 'Kart numarası zorunludur.';
        if (digits.length < 13 || digits.length > 19 || !passesLuhn(digits)) {
            return 'Geçerli bir kart numarası giriniz.';
        }
        return '';
    }

    function validateExpiry(value) {
        const match = /^(\d{2})\/(\d{2})$/.exec((value || '').trim());
        if (!match) return 'Son kullanma tarihini AA/YY formatında giriniz.';

        const month = parseInt(match[1], 10);
        const year = Math.floor(new Date().getFullYear() / 100) * 100 + parseInt(match[2], 10);
        if (month < 1 || month > 12) return 'Geçerli bir ay (01-12) giriniz.';

        const now = new Date();
        const currentYm = now.getFullYear() * 12 + (now.getMonth() + 1);
        const expiryYm = year * 12 + month;
        if (expiryYm < currentYm) return 'Kartın son kullanma tarihi geçmiş.';

        return '';
    }

    function validateCvv(value) {
        const digits = (value || '').replace(/\D/g, '');
        if (!/^\d{3,4}$/.test(digits)) return 'CVV 3 veya 4 haneli rakam olmalıdır.';
        return '';
    }

    function applyFieldState(input, errorMessage) {
        if (!input) return true;

        const feedback = input.parentElement?.querySelector('[data-valmsg-for], .text-danger');
        if (errorMessage) {
            input.classList.add('is-invalid');
            input.classList.remove('is-valid');
            if (feedback) feedback.textContent = errorMessage;
            return false;
        }

        input.classList.remove('is-invalid');
        input.classList.add('is-valid');
        if (feedback) feedback.textContent = '';
        return true;
    }

    function attach(formId) {
        const form = document.getElementById(formId);
        if (!form) return;

        const rules = [
            { input: document.getElementById('cardHolderInput'), validate: validateCardHolder },
            { input: document.getElementById('cardNumberInput'), validate: validateCardNumber },
            { input: document.getElementById('cardExpiryInput'), validate: validateExpiry },
            { input: document.getElementById('cardCvvInput'), validate: validateCvv }
        ].filter(r => r.input);

        rules.forEach(({ input, validate }) => {
            input.addEventListener('blur', () => applyFieldState(input, validate(input.value)));
            input.addEventListener('input', () => {
                if (input.classList.contains('is-invalid')) {
                    applyFieldState(input, validate(input.value));
                }
            });
        });

        form.addEventListener('submit', (e) => {
            let firstInvalid = null;
            for (const { input, validate } of rules) {
                const ok = applyFieldState(input, validate(input.value));
                if (!ok && !firstInvalid) firstInvalid = input;
            }

            if (firstInvalid) {
                e.preventDefault();
                e.stopImmediatePropagation();
                firstInvalid.focus();
                firstInvalid.scrollIntoView({ behavior: 'smooth', block: 'center' });
            }
        }, true);
    }

    return { attach, validateCardNumber, validateExpiry, validateCvv, validateCardHolder };
})();
