// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Cargo.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using System.Text;
namespace Content.Shared.Cargo
{
    [DataDefinition, NetSerializable, Serializable]
    public sealed partial class CargoOrderData
    {
        /// <summary>
        /// A unique (arbitrary) ID which identifies this order.
        /// </summary>
        [DataField]
        public int OrderId { get; private set; }

        /// <summary>
        /// The ID of the cargo product ordered.
        /// </summary>
        [DataField]
        public ProtoId<CargoProductPrototype> Product;

        /// <summary>
        ///     GoobStation - The cooldown in seconds before this product can be bought again.
        /// </summary>
        [DataField]
        public int Cooldown { get; private set; }

        /// <summary>
        /// The number of items in the order. Not readonly, as it might change
        /// due to caps on the amount of orders that can be placed.
        /// </summary>
        [DataField]
        public int OrderQuantity;

        /// <summary>
        /// How many instances of this order that we've already dispatched.
        /// </summary>
        [DataField]
        public int NumDispatched = 0;

        /// <summary>
        /// A string representation of the requestor's name.
        /// </summary>
        [DataField]
        public string Requester { get; private set; }
        // public String RequesterRank; // TODO Figure out how to get Character ID card data
        // public int RequesterId;
        public  bool Approved;

        // CorvaxGoob-CargoFeatures-Start
        [DataField]
        public string? DeliveryDestination { get; private set; }

        [DataField]
        public string? Note { get; private set; }

        public bool SecuredDelivery;
        // CorvaxGoob-CargoFeatures-End

        [DataField]
        public string? Approver;

        /// <summary>
        /// Which account to deduct funds from when ordering.
        /// </summary>
        [DataField]
        public ProtoId<CargoAccountPrototype> Account;

        // GoobStation - (cooldown parameter) cooldown on Cargo Orders (specifically gamba)
        // CorvaxGoob-CargoFeatures : Добавлен параметр доставки, заметки и защиты груза.
        public CargoOrderData(int orderId, string productId, string productName, int price, int amount, string requester, string? deliveryDestination, string? note, ProtoId<CargoAccountPrototype> account, int cooldown, bool securedDelivery = false)
        {
            OrderId = orderId;
            Product = product;
            OrderQuantity = amount;
            Requester = requester;
            // CorvaxGoob-CargoFeatures-Start
            DeliveryDestination = deliveryDestination;
            SecuredDelivery = securedDelivery;
            Note = note;
            // CorvaxGoob-CargoFeatures-End
            Account = account;
            // GoobStation - (cooldown assignment) cooldown on Cargo Orders (specifically gamba)
            Cooldown = cooldown;
        }

        /// <summary>
        /// Sets the approver for this order.
        /// </summary>
        /// <param name="approver">Nullable string representation of the approver's name.</param>
        public void SetApproverData(string? approver)
        {
            Approver = approver;
        }

        /// <summary>
        /// Sets the approver for this order by concatenating the name and job title.
        /// If both are null, will be an empty string.
        /// </summary>
        /// <param name="fullName">Nullable string representation of the approver's full name.</param>
        /// <param name="jobTitle">Nullable string representation of the approver's job title.</param>
        public void SetApproverData(string? fullName, string? jobTitle)
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(fullName))
            {
                sb.Append($"{fullName} ");
            }
            if (!string.IsNullOrWhiteSpace(jobTitle))
            {
                sb.Append($"({jobTitle})");
            }
            Approver = sb.ToString();
        }
    }
}
