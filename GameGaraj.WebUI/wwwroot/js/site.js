// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

// Helper functions to update badges
window.updateFavoritesBadge = function (delta) {
    var $navbarLinks = $('.js-favorites-link');

    $navbarLinks.each(function () {
        var $link = $(this);
        var $container = $link.find('.position-relative');
        var $badge = $container.find('.badge');

        if ($badge.length) {
            var currentCount = parseInt($badge.text()) || 0;
            var newCount = currentCount + delta;

            if (newCount > 0) {
                $badge.text(newCount);
            } else {
                $badge.remove();
            }
        } else if (delta > 0) {
            // Badge doesn't exist, create it matching _FavoritesCount partial view style
            $container.append('<span class="position-absolute badge rounded-circle d-flex align-items-center justify-content-center" style="top: -8px; right: -10px; background-color: #e74c3c; color: white; width: 18px; height: 18px; font-size: 0.7rem; border: 1px solid #232f3e;">1</span>');
        }
    });
};

window.updateBasketBadge = function (count) {
    var $navbarLinks = $('.js-basket-link');

    $navbarLinks.each(function () {
        var $link = $(this);
        var $container = $link.find('.position-relative');
        var $badge = $container.find('.badge');

        if ($badge.length) {
            if (count > 0) {
                $badge.text(count);
            } else {
                $badge.remove();
            }
        } else if (count > 0) {
            // Badge doesn't exist, create it matching _BasketCount partial view style
            $container.append('<span class="position-absolute badge rounded-circle d-flex align-items-center justify-content-center" style="top: -8px; right: -10px; background-color: #f68b1e; color: white; width: 18px; height: 18px; font-size: 0.7rem; border: 1px solid #232f3e;">' + count + '</span>');
        }
    });
};

$(document).ready(function () {
    function buildGoToBasketButton(buttonClassName) {
        var classes = buttonClassName || 'btn btn-secondary w-100 mt-auto position-relative';
        return '<a href="/Basket/Index" class="' + classes + '" style="z-index: 2; background-color: #28a745; border-color: #28a745; color: white;"><i class="fas fa-check me-2"></i>Sepete Git</a>';
    }

    // Generic AJAX Form Handler for Add to Cart
    $(document).on('submit', '.js-add-to-cart-form', function (e) {
        e.preventDefault();

        var $form = $(this);
        var $button = $form.find('button[type="submit"]');
        var originalContent = $button.html();

        // Disable button and show loading state
        $button.prop('disabled', true).html('<i class="fas fa-spinner fa-spin"></i>');

        $.ajax({
            url: $form.attr('action'),
            method: 'POST',
            data: $form.serialize(),
            headers: { "X-Requested-With": "XMLHttpRequest" },
            success: function (response) {
                if (response.success) {
                    // Show Toast
                    var toastEl = document.getElementById('liveToast');
                    if (toastEl) {
                        var toast = new bootstrap.Toast(toastEl, { delay: 3000 });
                        toast.show();
                    } else {
                        // Fallback alert if toast element is missing
                        // alert('Ürün sepete eklendi!');
                    }

                    // Update Basket Count Badge
                    if (response.count !== undefined) {
                        window.updateBasketBadge(response.count);
                    }

                    if ($form.closest('.product-card').length) {
                        $form.replaceWith(buildGoToBasketButton('btn btn-secondary w-100 mt-auto position-relative'));
                    } else if ($form.attr('id') === 'addToCartForm') {
                        var detailButtonClasses = ($button.attr('class') || 'add-to-cart-btn') + ' in-cart';
                        $form.replaceWith(buildGoToBasketButton(detailButtonClasses));
                    } else {
                        $button.prop('disabled', false).html(originalContent);
                        $button.addClass('in-cart');

                        var $icon = $button.find('i');
                        if ($icon.length) {
                            $icon.removeClass('fa-shopping-cart fa-cart-plus').addClass('fa-check');
                        }

                        var $textSpan = $button.find('span');
                        if ($textSpan.length) {
                            $textSpan.text('Sepete Git');
                        } else {
                            $button.html('<i class="fas fa-check"></i> Sepete Git');
                        }
                    }

                } else {
                    alert('Bir hata oluştu: ' + (response.message || 'Bilinmeyen hata'));
                    $button.prop('disabled', false).html(originalContent);
                }
            },
            error: function () {
                alert('Bir hata oluştu.');
                $button.prop('disabled', false).html(originalContent);
            }
        });
    });

    // Favorite Button Toggle Handler (Global)
    $(document).on('click', '.btn-favorite', function (e) {
        e.preventDefault();
        e.stopPropagation(); // Prevent card link from triggering

        var $button = $(this);
        var productId = $button.data('product-id');
        var $icon = $button.find('i');

        // Disable button temporarily
        $button.prop('disabled', true);

        // Determine action based on current state
        var isFavorite = $button.hasClass('active') || $icon.hasClass('fas');
        var url = isFavorite ? '/Favorites/Remove/' + productId : '/Favorites/Add/' + productId;

        // Get Verification Token if present
        var token = $('input[name="__RequestVerificationToken"]').val();

        $.ajax({
            url: url,
            method: 'POST',
            headers: {
                "X-Requested-With": "XMLHttpRequest",
                "RequestVerificationToken": token
            },
            success: function (response) {
                if (!response.success) {
                    console.error('Favori işlemi başarısız oldu.');
                    $button.prop('disabled', false);
                    return;
                }
                // Toggle visual state
                if ($button.hasClass('active')) {
                    $button.removeClass('active');
                    $icon.removeClass('fas').addClass('far');
                    $button.attr('title', 'Favorilere Ekle');
                    window.updateFavoritesBadge(-1);
                } else {
                    $button.addClass('active');
                    $icon.removeClass('far').addClass('fas');
                    $button.attr('title', 'Favorilerden Çıkar');
                    window.updateFavoritesBadge(1);

                    // Heartbeat animation
                    $icon.css('transform', 'scale(1.2)');
                    setTimeout(() => $icon.css('transform', 'scale(1)'), 200);
                }

                $button.prop('disabled', false);
            },
            error: function () {
                console.error('Favori işlemi başarısız oldu.');
                $button.prop('disabled', false);
            }
        });
    });

});
